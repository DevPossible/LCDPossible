using LCDPossible.Plugins.Thermalright.Protocol;
using LCDPossible.Core.Plugins;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Plugins.Thermalright.Handlers;

/// <summary>
/// Simulator handler for Trofeo Vision protocol.
/// Receives UDP packets, decodes the protocol, and emits frame data.
/// </summary>
public sealed class TrofeoVisionHandler : IVirtualDeviceHandler
{
    private readonly ILogger? _logger;
    private readonly List<byte> _buffer = [];
    private TrofeoVisionProtocol.HeaderInfo? _currentHeader;
    private bool _disposed;

    public TrofeoVisionHandler(ILogger? logger = null)
    {
        _logger = logger;
    }

    public string ProtocolId => "thermalright-trofeo-vision";

    public DeviceCapabilities Capabilities { get; } = new()
    {
        Width = TrofeoVisionProtocol.Width,
        Height = TrofeoVisionProtocol.Height,
        MaxPacketSize = TrofeoVisionProtocol.MaxPacketSize,
        MaxFrameRate = 60,
        SupportsBrightness = true,
        SupportsOrientation = true
    };

    public event EventHandler<DataProcessedEventArgs>? DataProcessed;

    public void ProcessData(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (data.Length == 0)
            return;

        // Check if this is a header packet (start of new frame)
        if (TrofeoVisionProtocol.IsHeaderPacket(data))
        {
            // Parse header
            _currentHeader = TrofeoVisionProtocol.ParseHeader(data);

            if (_currentHeader is null)
            {
                _logger?.LogWarning("Failed to parse protocol header");
                RaiseError("Invalid protocol header");
                return;
            }

            // Clear buffer and start accumulating
            _buffer.Clear();

            // Add data after header
            if (data.Length > TrofeoVisionProtocol.HeaderSize)
            {
                _buffer.AddRange(data[TrofeoVisionProtocol.HeaderSize..].ToArray());
            }

            _logger?.LogTrace(
                "New frame started: {Width}x{Height}, {Length} bytes expected, compression: {Compression}",
                _currentHeader.Width, _currentHeader.Height, _currentHeader.DataLength,
                _currentHeader.IsJpeg ? "JPEG" : "RGB565");
        }
        else if (_currentHeader != null)
        {
            // Continuation packet - add to buffer
            _buffer.AddRange(data.ToArray());
        }
        else
        {
            // No header yet - ignore
            _logger?.LogTrace("Received data without header, ignoring");
            return;
        }

        // Check if we have a complete frame
        if (_currentHeader != null && _buffer.Count >= _currentHeader.DataLength)
        {
            // Extract exactly the expected data length
            var frameData = _buffer.Take(_currentHeader.DataLength).ToArray();

            _logger?.LogTrace(
                "Frame complete: {Width}x{Height}, {Length} bytes, format: {Format}",
                _currentHeader.Width, _currentHeader.Height, frameData.Length,
                _currentHeader.IsJpeg ? "JPEG" : "RGB565");

            // Raise event
            DataProcessed?.Invoke(this, new DataProcessedEventArgs
            {
                FrameData = frameData,
                Format = _currentHeader.IsJpeg ? "jpeg" : "rgb565",
                Width = _currentHeader.Width,
                Height = _currentHeader.Height,
                CustomData = new FrameInfo
                {
                    Command = _currentHeader.Command,
                    Compression = _currentHeader.Compression,
                    DeclaredLength = _currentHeader.DataLength,
                    ActualLength = frameData.Length
                }
            });

            // Reset for next frame
            _currentHeader = null;
            _buffer.Clear();
        }
    }

    public void Reset()
    {
        _currentHeader = null;
        _buffer.Clear();
        _logger?.LogDebug("Handler state reset");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Reset();
        _disposed = true;
    }

    private void RaiseError(string message)
    {
        DataProcessed?.Invoke(this, new DataProcessedEventArgs
        {
            Error = message
        });
    }

    /// <summary>
    /// Additional frame metadata.
    /// </summary>
    public sealed class FrameInfo
    {
        public byte Command { get; init; }
        public byte Compression { get; init; }
        public int DeclaredLength { get; init; }
        public int ActualLength { get; init; }
    }
}
