using System.Net;
using System.Net.Sockets;
using LCDPossible.VirtualLcd.Protocols;

namespace LCDPossible.VirtualLcd.Network;

/// <summary>
/// Receives HID packets over UDP and processes them through a protocol.
/// </summary>
public sealed class UdpHidReceiver : IDisposable
{
    private readonly ILcdProtocol _protocol;
    private readonly IPEndPoint _listenEndpoint;
    private UdpClient? _client;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private bool _disposed;

    // Statistics
    private long _packetsReceived;
    private long _bytesReceived;
    private long _framesReceived;
    private DateTime _startTime;

    /// <summary>
    /// Creates a new UDP HID receiver.
    /// </summary>
    /// <param name="protocol">Protocol to use for parsing packets.</param>
    /// <param name="bindAddress">IP address to bind to.</param>
    /// <param name="port">UDP port to listen on.</param>
    public UdpHidReceiver(ILcdProtocol protocol, IPAddress bindAddress, int port)
    {
        _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        _listenEndpoint = new IPEndPoint(bindAddress, port);
    }

    /// <summary>
    /// Creates a new UDP HID receiver with default bind address (any).
    /// </summary>
    public UdpHidReceiver(ILcdProtocol protocol, int port)
        : this(protocol, IPAddress.Any, port)
    {
    }

    /// <summary>
    /// Event raised when a complete frame is received.
    /// </summary>
    public event EventHandler<FrameReceivedEventArgs>? FrameReceived;

    /// <summary>
    /// Event raised when an error occurs.
    /// </summary>
    public event EventHandler<ReceiverErrorEventArgs>? Error;

    /// <summary>
    /// Event raised when receiver statistics are updated.
    /// </summary>
    public event EventHandler<ReceiverStatsEventArgs>? StatsUpdated;

    /// <summary>
    /// Gets whether the receiver is currently running.
    /// </summary>
    public bool IsRunning => _receiveTask != null && !_receiveTask.IsCompleted;

    /// <summary>
    /// Gets the endpoint being listened on.
    /// </summary>
    public IPEndPoint ListenEndpoint => _listenEndpoint;

    /// <summary>
    /// Gets current receiver statistics.
    /// </summary>
    public ReceiverStats GetStats()
    {
        var elapsed = _startTime == default ? TimeSpan.Zero : DateTime.UtcNow - _startTime;
        return new ReceiverStats
        {
            PacketsReceived = Interlocked.Read(ref _packetsReceived),
            BytesReceived = Interlocked.Read(ref _bytesReceived),
            FramesReceived = Interlocked.Read(ref _framesReceived),
            Elapsed = elapsed,
            ProtocolStats = _protocol.GetStats()
        };
    }

    /// <summary>
    /// Start receiving packets.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsRunning)
        {
            return;
        }

        _client = new UdpClient(_listenEndpoint);
        _cts = new CancellationTokenSource();
        _startTime = DateTime.UtcNow;
        _protocol.Reset();

        _receiveTask = ReceiveLoopAsync(_cts.Token);
    }

    /// <summary>
    /// Stop receiving packets.
    /// </summary>
    public async Task StopAsync()
    {
        if (!IsRunning || _cts == null || _receiveTask == null)
        {
            return;
        }

        await _cts.CancelAsync();

        try
        {
            await _receiveTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _client?.Close();
        _client?.Dispose();
        _client = null;
        _cts.Dispose();
        _cts = null;
        _receiveTask = null;
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        if (_client == null)
        {
            return;
        }

        var statsUpdateInterval = TimeSpan.FromMilliseconds(250);
        var lastStatsUpdate = DateTime.UtcNow;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try
                {
                    result = await _client.ReceiveAsync(ct).ConfigureAwait(false);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
                {
                    // Socket was closed
                    break;
                }

                Interlocked.Increment(ref _packetsReceived);
                Interlocked.Add(ref _bytesReceived, result.Buffer.Length);

                // Process through protocol
                var frameResult = _protocol.ProcessHidReport(result.Buffer);

                if (frameResult.IsComplete && frameResult.ImageData != null)
                {
                    Interlocked.Increment(ref _framesReceived);
                    OnFrameReceived(frameResult.ImageData, frameResult.Format);
                }
                else if (frameResult.Error != null)
                {
                    OnError(frameResult.Error);
                }

                // Periodic stats update
                var now = DateTime.UtcNow;
                if (now - lastStatsUpdate >= statsUpdateInterval)
                {
                    OnStatsUpdated();
                    lastStatsUpdate = now;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
        catch (Exception ex)
        {
            OnError($"Receive loop error: {ex.Message}");
        }
    }

    private void OnFrameReceived(byte[] imageData, ImageFormat format)
    {
        FrameReceived?.Invoke(this, new FrameReceivedEventArgs(imageData, format));
    }

    private void OnError(string message)
    {
        Error?.Invoke(this, new ReceiverErrorEventArgs(message));
    }

    private void OnStatsUpdated()
    {
        StatsUpdated?.Invoke(this, new ReceiverStatsEventArgs(GetStats()));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cts?.Cancel();
        _client?.Close();
        _client?.Dispose();
        _cts?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Event args for frame received event.
/// </summary>
public sealed class FrameReceivedEventArgs : EventArgs
{
    public FrameReceivedEventArgs(byte[] imageData, ImageFormat format)
    {
        ImageData = imageData;
        Format = format;
    }

    /// <summary>
    /// The decoded image data.
    /// </summary>
    public byte[] ImageData { get; }

    /// <summary>
    /// Format of the image data.
    /// </summary>
    public ImageFormat Format { get; }
}

/// <summary>
/// Event args for receiver error event.
/// </summary>
public sealed class ReceiverErrorEventArgs : EventArgs
{
    public ReceiverErrorEventArgs(string message)
    {
        Message = message;
    }

    /// <summary>
    /// Error message.
    /// </summary>
    public string Message { get; }
}

/// <summary>
/// Event args for stats updated event.
/// </summary>
public sealed class ReceiverStatsEventArgs : EventArgs
{
    public ReceiverStatsEventArgs(ReceiverStats stats)
    {
        Stats = stats;
    }

    /// <summary>
    /// Current receiver statistics.
    /// </summary>
    public ReceiverStats Stats { get; }
}

/// <summary>
/// Receiver statistics.
/// </summary>
public record ReceiverStats
{
    /// <summary>
    /// Total UDP packets received.
    /// </summary>
    public long PacketsReceived { get; init; }

    /// <summary>
    /// Total bytes received.
    /// </summary>
    public long BytesReceived { get; init; }

    /// <summary>
    /// Total complete frames received.
    /// </summary>
    public long FramesReceived { get; init; }

    /// <summary>
    /// Time since receiver started.
    /// </summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>
    /// Protocol-level statistics.
    /// </summary>
    public ProtocolStats? ProtocolStats { get; init; }

    /// <summary>
    /// Calculated frames per second.
    /// </summary>
    public double Fps => Elapsed.TotalSeconds > 0 ? FramesReceived / Elapsed.TotalSeconds : 0;

    /// <summary>
    /// Calculated bytes per second.
    /// </summary>
    public double BytesPerSecond => Elapsed.TotalSeconds > 0 ? BytesReceived / Elapsed.TotalSeconds : 0;
}
