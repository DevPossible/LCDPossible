using System.Buffers;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using LCDPossible.Core.Ipc;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Ipc;

/// <summary>
/// IPC server that listens for commands from CLI clients.
/// Uses named pipes on Windows and Unix domain sockets on Linux/macOS.
/// </summary>
public sealed class IpcServer : IIpcServer
{
    private readonly ILogger<IpcServer>? _logger;
    private readonly CancellationTokenSource _serverCts = new();
    private Task? _serverTask;
    private Socket? _unixSocket;
    private bool _isRunning;
    private bool _disposed;

    public bool IsRunning => _isRunning;

    public event EventHandler<IpcRequestEventArgs>? RequestReceived;

    public IpcServer(ILogger<IpcServer>? logger = null)
    {
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_isRunning)
            return;

        _logger?.LogInformation("Starting IPC server at {Path}", IpcPaths.GetFullPipePath());

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _serverTask = RunWindowsServerAsync(_serverCts.Token);
        }
        else
        {
            _serverTask = RunUnixServerAsync(_serverCts.Token);
        }

        _isRunning = true;
        _logger?.LogInformation("IPC server started");

        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_isRunning)
            return;

        _logger?.LogInformation("Stopping IPC server...");

        _serverCts.Cancel();

        // Close Unix socket to unblock Accept
        if (_unixSocket is not null)
        {
            try
            {
                _unixSocket.Close();
            }
            catch
            {
                // Ignore close errors
            }
        }

        if (_serverTask is not null)
        {
            try
            {
                await _serverTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger?.LogWarning("IPC server did not stop gracefully within timeout");
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        // Cleanup Unix socket file
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var socketPath = IpcPaths.GetPipePath();
            try
            {
                if (File.Exists(socketPath))
                    File.Delete(socketPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        _isRunning = false;
        _logger?.LogInformation("IPC server stopped");
    }

    private async Task RunWindowsServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? pipeServer = null;
            try
            {
                pipeServer = new NamedPipeServerStream(
                    IpcPaths.PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                _logger?.LogDebug("Waiting for IPC client connection...");
                await pipeServer.WaitForConnectionAsync(cancellationToken);
                _logger?.LogDebug("IPC client connected");

                // Handle client in background, continue accepting
                var clientPipe = pipeServer;
                pipeServer = null; // Prevent disposal in finally
                _ = HandleClientAsync(clientPipe, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error accepting IPC connection");
                await Task.Delay(100, cancellationToken);
            }
            finally
            {
                pipeServer?.Dispose();
            }
        }
    }

    private async Task RunUnixServerAsync(CancellationToken cancellationToken)
    {
        var socketPath = IpcPaths.GetPipePath();

        // Cleanup stale socket file
        IpcPaths.CleanupStaleSocket();

        _unixSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

        try
        {
            _unixSocket.Bind(new UnixDomainSocketEndPoint(socketPath));
            _unixSocket.Listen(10);

            _logger?.LogDebug("Unix socket bound to {Path}", socketPath);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _logger?.LogDebug("Waiting for IPC client connection...");
                    var clientSocket = await _unixSocket.AcceptAsync(cancellationToken);
                    _logger?.LogDebug("IPC client connected");

                    // Handle client in background, continue accepting
                    _ = HandleUnixClientAsync(clientSocket, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
                {
                    // Socket was closed, exit gracefully
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error accepting Unix socket connection");
                    await Task.Delay(100, cancellationToken);
                }
            }
        }
        finally
        {
            _unixSocket.Close();
            _unixSocket.Dispose();
            _unixSocket = null;

            // Remove socket file
            try
            {
                if (File.Exists(socketPath))
                    File.Delete(socketPath);
            }
            catch
            {
                // Ignore
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        try
        {
            await using var _ = pipe;
            using var networkStream = new BufferedStream(pipe);

            while (pipe.IsConnected && !cancellationToken.IsCancellationRequested)
            {
                var request = await ReadRequestAsync(networkStream, cancellationToken);
                if (request is null)
                {
                    // Client disconnected
                    break;
                }

                _logger?.LogDebug("Received IPC request: {Command}", request.Command);

                await ProcessRequestAsync(request, networkStream, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected
        }
        catch (IOException)
        {
            // Client disconnected
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling IPC client");
        }
    }

    private async Task HandleUnixClientAsync(Socket clientSocket, CancellationToken cancellationToken)
    {
        try
        {
            using var _ = clientSocket;
            await using var networkStream = new NetworkStream(clientSocket, ownsSocket: false);

            while (clientSocket.Connected && !cancellationToken.IsCancellationRequested)
            {
                var request = await ReadRequestAsync(networkStream, cancellationToken);
                if (request is null)
                {
                    // Client disconnected
                    break;
                }

                _logger?.LogDebug("Received IPC request: {Command}", request.Command);

                await ProcessRequestAsync(request, networkStream, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected
        }
        catch (IOException)
        {
            // Client disconnected
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling Unix IPC client");
        }
    }

    private async Task ProcessRequestAsync(IpcRequest request, Stream stream, CancellationToken cancellationToken)
    {
        IpcResponse response;

        try
        {
            var tcs = new TaskCompletionSource<IpcResponse>();

            var args = new IpcRequestEventArgs
            {
                Request = request,
                SendResponseAsync = (resp, ct) =>
                {
                    tcs.TrySetResult(resp);
                    return Task.CompletedTask;
                }
            };

            RequestReceived?.Invoke(this, args);

            // Wait for response with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

            try
            {
                response = await tcs.Task.WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                response = IpcResponse.Fail(request.Id, IpcErrorCodes.Timeout, "Request processing timed out");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing IPC request {Command}", request.Command);
            response = IpcResponse.Fail(request.Id, ex);
        }

        await WriteResponseAsync(stream, response, cancellationToken);
    }

    private static async Task<IpcRequest?> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
    {
        // Read 4-byte length prefix (little-endian)
        var lengthBuffer = new byte[4];
        var bytesRead = await stream.ReadAsync(lengthBuffer.AsMemory(), cancellationToken);
        if (bytesRead == 0)
            return null; // Client disconnected

        if (bytesRead < 4)
        {
            // Read remaining bytes
            var remaining = 4 - bytesRead;
            var additionalRead = await stream.ReadAsync(lengthBuffer.AsMemory(bytesRead, remaining), cancellationToken);
            if (additionalRead < remaining)
                return null;
        }

        var messageLength = BitConverter.ToInt32(lengthBuffer, 0);
        if (messageLength <= 0 || messageLength > 1024 * 1024) // Max 1MB
            throw new IpcException($"Invalid message length: {messageLength}");

        // Read message body
        var buffer = ArrayPool<byte>.Shared.Rent(messageLength);
        try
        {
            var totalRead = 0;
            while (totalRead < messageLength)
            {
                var chunk = await stream.ReadAsync(buffer.AsMemory(totalRead, messageLength - totalRead), cancellationToken);
                if (chunk == 0)
                    return null;
                totalRead += chunk;
            }

            var json = Encoding.UTF8.GetString(buffer, 0, messageLength);
            return JsonSerializer.Deserialize<IpcRequest>(json, IpcJsonOptions.Default);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task WriteResponseAsync(Stream stream, IpcResponse response, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(response, IpcJsonOptions.Default);
        var messageBytes = Encoding.UTF8.GetBytes(json);
        var lengthBytes = BitConverter.GetBytes(messageBytes.Length);

        await stream.WriteAsync(lengthBytes, cancellationToken);
        await stream.WriteAsync(messageBytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _serverCts.Cancel();
        _serverCts.Dispose();
        _unixSocket?.Dispose();
    }
}
