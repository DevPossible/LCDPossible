using LCDPossible.Core.Plugins;

namespace LCDPossible.VirtualLcd.Protocols;

/// <summary>
/// Adapts an IVirtualDeviceHandler from a device plugin to ILcdProtocol for the simulator.
/// </summary>
public sealed class PluginProtocolAdapter : ILcdProtocol, IDisposable
{
    private readonly IVirtualDeviceHandler _handler;
    private readonly DeviceProtocolInfo _protocolInfo;

    private long _packetsReceived;
    private long _bytesReceived;
    private long _framesDecoded;
    private long _errorPackets;
    private long _droppedFrames;
    private DateTime? _lastFrameTime;
    private int _lastFrameSize;

    // Buffer for the current frame being assembled
    private byte[]? _pendingFrame;
    private string? _pendingFormat;
    private bool _frameReady;

    public PluginProtocolAdapter(IVirtualDeviceHandler handler, DeviceProtocolInfo protocolInfo)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _protocolInfo = protocolInfo ?? throw new ArgumentNullException(nameof(protocolInfo));

        // Subscribe to handler events
        _handler.DataProcessed += OnDataProcessed;
    }

    public string ProtocolId => _protocolInfo.ProtocolId;

    public string DisplayName => _protocolInfo.DisplayName;

    public string Description => $"Plugin-provided protocol: {_protocolInfo.DisplayName}";

    public int Width => _protocolInfo.Capabilities.Width;

    public int Height => _protocolInfo.Capabilities.Height;

    public int HidReportSize => _protocolInfo.Capabilities.MaxPacketSize + 1; // +1 for report ID

    // These could be fetched from plugin metadata if needed
    public ushort VendorId => 0;
    public ushort ProductId => 0;

    public FrameResult ProcessHidReport(ReadOnlySpan<byte> hidReport)
    {
        _packetsReceived++;
        _bytesReceived += hidReport.Length;

        try
        {
            // Clear any previous frame
            _frameReady = false;
            _pendingFrame = null;

            // Process through the plugin handler
            _handler.ProcessData(hidReport);

            // Check if handler produced a frame
            if (_frameReady && _pendingFrame is not null)
            {
                _framesDecoded++;
                _lastFrameTime = DateTime.UtcNow;
                _lastFrameSize = _pendingFrame.Length;

                var format = _pendingFormat switch
                {
                    "jpeg" => ImageFormat.Jpeg,
                    "rgb565" => ImageFormat.Rgb565,
                    "rgb888" => ImageFormat.Rgb888,
                    _ => ImageFormat.Jpeg
                };

                return FrameResult.Complete(_pendingFrame, format);
            }

            return FrameResult.Incomplete();
        }
        catch (Exception ex)
        {
            _errorPackets++;
            return FrameResult.Failed($"Plugin error: {ex.Message}");
        }
    }

    public void Reset()
    {
        // Track dropped frames if we had a pending frame
        if (_frameReady && _pendingFrame != null)
        {
            _droppedFrames++;
        }

        _handler.Reset();
        _frameReady = false;
        _pendingFrame = null;
    }

    public ProtocolStats GetStats()
    {
        return new ProtocolStats
        {
            PacketsReceived = _packetsReceived,
            BytesReceived = _bytesReceived,
            FramesDecoded = _framesDecoded,
            ErrorPackets = _errorPackets,
            DroppedFrames = _droppedFrames,
            LastFrameTime = _lastFrameTime,
            LastFrameSize = _lastFrameSize
        };
    }

    public void Dispose()
    {
        _handler.DataProcessed -= OnDataProcessed;
        _handler.Dispose();
    }

    private void OnDataProcessed(object? sender, DataProcessedEventArgs e)
    {
        if (e.IsSuccess && e.FrameData.HasValue)
        {
            _pendingFrame = e.FrameData.Value.ToArray();
            _pendingFormat = e.Format;
            _frameReady = true;
        }
        else if (!e.IsSuccess)
        {
            _errorPackets++;
        }
    }
}
