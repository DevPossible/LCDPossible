using System.Buffers;

namespace LCDPossible.VirtualLcd.Protocols;

/// <summary>
/// Base class for LCD protocols with common packet accumulation logic.
/// </summary>
public abstract class LcdProtocolBase : ILcdProtocol
{
    private byte[]? _frameBuffer;
    private int _frameBufferPosition;
    private int _expectedFrameLength;
    private int _packetsInFrame;

    // Statistics
    private long _packetsReceived;
    private long _bytesReceived;
    private long _framesDecoded;
    private long _errorPackets;
    private long _droppedFrames;
    private DateTime? _lastFrameTime;
    private int _lastFrameSize;

    /// <inheritdoc />
    public abstract string ProtocolId { get; }

    /// <inheritdoc />
    public abstract string DisplayName { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public abstract int Width { get; }

    /// <inheritdoc />
    public abstract int Height { get; }

    /// <inheritdoc />
    public abstract int HidReportSize { get; }

    /// <inheritdoc />
    public abstract ushort VendorId { get; }

    /// <inheritdoc />
    public abstract ushort ProductId { get; }

    /// <summary>
    /// Size of the protocol header in the first packet (bytes after report ID).
    /// </summary>
    protected abstract int HeaderSize { get; }

    /// <summary>
    /// Data bytes per packet (excluding report ID).
    /// </summary>
    protected virtual int DataPerPacket => HidReportSize - 1;

    /// <inheritdoc />
    public virtual void Reset()
    {
        if (_frameBuffer != null && _frameBufferPosition > 0)
        {
            // We had an incomplete frame
            Interlocked.Increment(ref _droppedFrames);
        }

        if (_frameBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(_frameBuffer);
        }

        _frameBuffer = null;
        _frameBufferPosition = 0;
        _expectedFrameLength = 0;
        _packetsInFrame = 0;
    }

    /// <inheritdoc />
    public virtual void Dispose()
    {
        if (_frameBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(_frameBuffer);
            _frameBuffer = null;
        }
    }

    /// <inheritdoc />
    public ProtocolStats GetStats() => new()
    {
        PacketsReceived = Interlocked.Read(ref _packetsReceived),
        BytesReceived = Interlocked.Read(ref _bytesReceived),
        FramesDecoded = Interlocked.Read(ref _framesDecoded),
        ErrorPackets = Interlocked.Read(ref _errorPackets),
        DroppedFrames = Interlocked.Read(ref _droppedFrames),
        LastFrameTime = _lastFrameTime,
        LastFrameSize = _lastFrameSize
    };

    /// <inheritdoc />
    public FrameResult ProcessHidReport(ReadOnlySpan<byte> hidReport)
    {
        Interlocked.Increment(ref _packetsReceived);
        Interlocked.Add(ref _bytesReceived, hidReport.Length);

        // Validate packet size
        if (hidReport.Length != HidReportSize)
        {
            Interlocked.Increment(ref _errorPackets);
            return FrameResult.Failed($"Invalid packet size: expected {HidReportSize}, got {hidReport.Length}");
        }

        // Skip report ID (first byte), get data portion
        var data = hidReport.Slice(1);

        // Check if this is the start of a new frame
        if (IsFrameStart(data))
        {
            // If we had an incomplete frame, count it as dropped
            if (_frameBuffer != null && _frameBufferPosition > 0 && _frameBufferPosition < _expectedFrameLength)
            {
                Interlocked.Increment(ref _droppedFrames);
            }

            // Parse header to get frame info
            var headerResult = ParseHeader(data);
            if (headerResult.Error != null)
            {
                Interlocked.Increment(ref _errorPackets);
                Reset();
                return FrameResult.Failed(headerResult.Error);
            }

            // Initialize buffer for new frame
            _expectedFrameLength = headerResult.ImageDataLength;
            _frameBuffer = ArrayPool<byte>.Shared.Rent(_expectedFrameLength);
            _frameBufferPosition = 0;
            _packetsInFrame = 0;

            // Copy image data from first packet (after header)
            var imageDataInFirstPacket = data.Slice(HeaderSize);
            var bytesToCopy = Math.Min(imageDataInFirstPacket.Length, _expectedFrameLength);
            imageDataInFirstPacket.Slice(0, bytesToCopy).CopyTo(_frameBuffer);
            _frameBufferPosition = bytesToCopy;
            _packetsInFrame = 1;

            // Check if frame is complete (small frames might fit in one packet)
            if (_frameBufferPosition >= _expectedFrameLength)
            {
                return CompleteFrame(headerResult.Format);
            }

            return FrameResult.Incomplete(_packetsInFrame, _expectedFrameLength, _frameBufferPosition);
        }

        // Continuation packet
        if (_frameBuffer == null)
        {
            // We received a continuation without a start - ignore or error
            Interlocked.Increment(ref _errorPackets);
            return FrameResult.Failed("Received continuation packet without frame start");
        }

        // Append data to buffer
        var remainingSpace = _expectedFrameLength - _frameBufferPosition;
        var bytesToAppend = Math.Min(data.Length, remainingSpace);
        data.Slice(0, bytesToAppend).CopyTo(_frameBuffer.AsSpan(_frameBufferPosition));
        _frameBufferPosition += bytesToAppend;
        _packetsInFrame++;

        // Check if frame is complete
        if (_frameBufferPosition >= _expectedFrameLength)
        {
            // Determine format from header (stored during ParseHeader)
            return CompleteFrame(GetCurrentFrameFormat());
        }

        return FrameResult.Incomplete(_packetsInFrame, _expectedFrameLength, _frameBufferPosition);
    }

    /// <summary>
    /// Check if the data portion of a packet indicates the start of a new frame.
    /// </summary>
    /// <param name="data">Packet data (excluding report ID).</param>
    /// <returns>True if this is a frame start packet.</returns>
    protected abstract bool IsFrameStart(ReadOnlySpan<byte> data);

    /// <summary>
    /// Parse the protocol header from the first packet.
    /// </summary>
    /// <param name="data">Packet data (excluding report ID).</param>
    /// <returns>Header parse result with image data length and format.</returns>
    protected abstract HeaderParseResult ParseHeader(ReadOnlySpan<byte> data);

    /// <summary>
    /// Get the image format for the current frame being assembled.
    /// Called when completing a frame that spans multiple packets.
    /// </summary>
    protected abstract ImageFormat GetCurrentFrameFormat();

    /// <summary>
    /// Complete the current frame and return it.
    /// </summary>
    private FrameResult CompleteFrame(ImageFormat format)
    {
        if (_frameBuffer == null)
        {
            return FrameResult.Failed("No frame buffer");
        }

        // Extract the exact frame data
        var frameData = new byte[_expectedFrameLength];
        Array.Copy(_frameBuffer, frameData, _expectedFrameLength);

        // Return buffer to pool
        ArrayPool<byte>.Shared.Return(_frameBuffer);

        // Update stats
        Interlocked.Increment(ref _framesDecoded);
        _lastFrameTime = DateTime.UtcNow;
        _lastFrameSize = _expectedFrameLength;

        // Reset state for next frame
        _frameBuffer = null;
        _frameBufferPosition = 0;
        _expectedFrameLength = 0;
        _packetsInFrame = 0;

        return FrameResult.Complete(frameData, format);
    }

    /// <summary>
    /// Result of parsing a protocol header.
    /// </summary>
    protected readonly record struct HeaderParseResult
    {
        /// <summary>
        /// Length of the image data in bytes.
        /// </summary>
        public int ImageDataLength { get; init; }

        /// <summary>
        /// Format of the image data.
        /// </summary>
        public ImageFormat Format { get; init; }

        /// <summary>
        /// Error message if parsing failed.
        /// </summary>
        public string? Error { get; init; }

        public static HeaderParseResult Success(int length, ImageFormat format) =>
            new() { ImageDataLength = length, Format = format };

        public static HeaderParseResult Failure(string error) =>
            new() { Error = error };
    }
}
