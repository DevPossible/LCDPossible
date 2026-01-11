using System.Buffers;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using LCDPossible.Core.Ipc;

namespace LCDPossible.Ipc;

/// <summary>
/// IPC client for sending commands to the running LCDPossible service.
/// Uses named pipes on Windows and Unix domain sockets on Linux/macOS.
/// </summary>
public sealed class IpcClient : IIpcClient
{
    private Stream? _stream;
    private NamedPipeClientStream? _pipeClient;
    private Socket? _unixSocket;
    private bool _disposed;

    public bool IsConnected => _stream is not null &&
        ((_pipeClient?.IsConnected ?? false) || (_unixSocket?.Connected ?? false));

    public async Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (IsConnected)
            return;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await ConnectWindowsAsync(timeoutCts.Token);
            }
            else
            {
                await ConnectUnixAsync(timeoutCts.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Connection to LCDPossible service timed out after {timeout.TotalSeconds:F1}s");
        }
        catch (IOException ex)
        {
            throw new IpcException("Failed to connect to LCDPossible service", IpcErrorCodes.ServiceNotRunning, ex);
        }
        catch (SocketException ex)
        {
            throw new IpcException("Failed to connect to LCDPossible service", IpcErrorCodes.ServiceNotRunning, ex);
        }
    }

    private async Task ConnectWindowsAsync(CancellationToken cancellationToken)
    {
        _pipeClient = new NamedPipeClientStream(
            ".",
            IpcPaths.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await _pipeClient.ConnectAsync(cancellationToken);
        _stream = new BufferedStream(_pipeClient);
    }

    private async Task ConnectUnixAsync(CancellationToken cancellationToken)
    {
        var socketPath = IpcPaths.GetPipePath();
        if (!File.Exists(socketPath))
        {
            throw new IpcException(
                "LCDPossible service is not running (socket file not found)",
                IpcErrorCodes.ServiceNotRunning);
        }

        _unixSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await _unixSocket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken);
        _stream = new NetworkStream(_unixSocket, ownsSocket: false);
    }

    public async Task<IpcResponse> SendAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        if (!IsConnected || _stream is null)
            throw new IpcException("Not connected to service", IpcErrorCodes.ServiceNotRunning);

        try
        {
            await WriteRequestAsync(_stream, request, cancellationToken);
            var response = await ReadResponseAsync(_stream, cancellationToken);

            if (response is null)
            {
                throw new IpcException("Service disconnected unexpectedly", IpcErrorCodes.InternalError);
            }

            return response;
        }
        catch (IOException ex)
        {
            throw new IpcException("Communication error with service", IpcErrorCodes.InternalError, ex);
        }
    }

    public void Disconnect()
    {
        _stream?.Dispose();
        _stream = null;

        _pipeClient?.Dispose();
        _pipeClient = null;

        _unixSocket?.Dispose();
        _unixSocket = null;
    }

    private static async Task WriteRequestAsync(Stream stream, IpcRequest request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request, IpcJsonOptions.Default);
        var messageBytes = Encoding.UTF8.GetBytes(json);
        var lengthBytes = BitConverter.GetBytes(messageBytes.Length);

        await stream.WriteAsync(lengthBytes, cancellationToken);
        await stream.WriteAsync(messageBytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<IpcResponse?> ReadResponseAsync(Stream stream, CancellationToken cancellationToken)
    {
        // Read 4-byte length prefix (little-endian)
        var lengthBuffer = new byte[4];
        var bytesRead = await ReadFullAsync(stream, lengthBuffer, cancellationToken);
        if (bytesRead == 0)
            return null; // Server disconnected

        var messageLength = BitConverter.ToInt32(lengthBuffer, 0);
        if (messageLength <= 0 || messageLength > 1024 * 1024) // Max 1MB
            throw new IpcException($"Invalid response length: {messageLength}");

        // Read message body
        var buffer = ArrayPool<byte>.Shared.Rent(messageLength);
        try
        {
            var totalRead = await ReadFullAsync(stream, buffer.AsMemory(0, messageLength), cancellationToken);
            if (totalRead < messageLength)
                return null;

            var json = Encoding.UTF8.GetString(buffer, 0, messageLength);
            return JsonSerializer.Deserialize<IpcResponse>(json, IpcJsonOptions.Default);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<int> ReadFullAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var chunk = await stream.ReadAsync(buffer[totalRead..], cancellationToken);
            if (chunk == 0)
                return totalRead;
            totalRead += chunk;
        }
        return totalRead;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Disconnect();
    }
}

/// <summary>
/// Helper class for common IPC client operations.
/// </summary>
public static class IpcClientHelper
{
    /// <summary>
    /// Default connection timeout.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Sends a command to the running service and returns the response.
    /// </summary>
    /// <param name="command">Command name.</param>
    /// <param name="args">Optional arguments.</param>
    /// <param name="timeout">Connection timeout.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response from the service.</returns>
    public static async Task<IpcResponse> SendCommandAsync(
        string command,
        Dictionary<string, string>? args = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        using var client = new IpcClient();
        await client.ConnectAsync(timeout ?? DefaultTimeout, cancellationToken);

        var request = IpcRequest.Create(command, args);
        return await client.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Checks if the service is running and returns status information.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Service status, or null if service is not running.</returns>
    public static async Task<ServiceStatus?> GetServiceStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!IpcPaths.IsServiceRunning())
            return null;

        try
        {
            var response = await SendCommandAsync("status", cancellationToken: cancellationToken);
            if (response.Success && response.Data is JsonElement element)
            {
                return JsonSerializer.Deserialize<ServiceStatus>(element.GetRawText(), IpcJsonOptions.Default);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}
