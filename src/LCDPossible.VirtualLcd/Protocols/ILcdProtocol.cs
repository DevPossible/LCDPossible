namespace LCDPossible.VirtualLcd.Protocols;

/// <summary>
/// Defines how to parse HID packets for a specific LCD device.
/// Each protocol implementation handles the device-specific packet format.
/// </summary>
public interface ILcdProtocol : IDisposable
{
    /// <summary>
    /// Protocol identifier (e.g., "trofeo-vision", "pa120-digital").
    /// Used for CLI selection and configuration.
    /// </summary>
    string ProtocolId { get; }

    /// <summary>
    /// Human-readable name for display in UI.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Brief description of the protocol/device.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Display width in pixels.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Display height in pixels.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Expected HID report size in bytes (e.g., 513 for Trofeo Vision, 65 for PA120).
    /// </summary>
    int HidReportSize { get; }

    /// <summary>
    /// USB Vendor ID of the device being simulated.
    /// </summary>
    ushort VendorId { get; }

    /// <summary>
    /// USB Product ID of the device being simulated.
    /// </summary>
    ushort ProductId { get; }

    /// <summary>
    /// Process an incoming HID report. Call this for each UDP packet received.
    /// </summary>
    /// <param name="hidReport">Raw HID report data (including report ID byte).</param>
    /// <returns>Frame result indicating completion status and decoded data.</returns>
    FrameResult ProcessHidReport(ReadOnlySpan<byte> hidReport);

    /// <summary>
    /// Reset protocol state. Call on connection, timeout, or error recovery.
    /// </summary>
    void Reset();

    /// <summary>
    /// Get current protocol statistics.
    /// </summary>
    ProtocolStats GetStats();
}

/// <summary>
/// Result of processing an HID report.
/// </summary>
public readonly record struct FrameResult
{
    /// <summary>
    /// True when a complete frame has been assembled and is ready for display.
    /// </summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// Decoded image data (JPEG or raw pixels). Only valid when IsComplete is true.
    /// </summary>
    public byte[]? ImageData { get; init; }

    /// <summary>
    /// Image format of the decoded data.
    /// </summary>
    public ImageFormat Format { get; init; }

    /// <summary>
    /// Error message if parsing failed. Null on success.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Number of packets accumulated so far for current frame.
    /// </summary>
    public int PacketsInFrame { get; init; }

    /// <summary>
    /// Expected total bytes for current frame (from header). 0 if unknown.
    /// </summary>
    public int ExpectedBytes { get; init; }

    /// <summary>
    /// Bytes received so far for current frame.
    /// </summary>
    public int ReceivedBytes { get; init; }

    /// <summary>
    /// Create an incomplete result (more packets needed).
    /// </summary>
    public static FrameResult Incomplete(int packetsInFrame = 0, int expectedBytes = 0, int receivedBytes = 0) =>
        new()
        {
            IsComplete = false,
            PacketsInFrame = packetsInFrame,
            ExpectedBytes = expectedBytes,
            ReceivedBytes = receivedBytes
        };

    /// <summary>
    /// Create a complete result with image data.
    /// </summary>
    public static FrameResult Complete(byte[] data, ImageFormat format) =>
        new()
        {
            IsComplete = true,
            ImageData = data,
            Format = format
        };

    /// <summary>
    /// Create a failed result with error message.
    /// </summary>
    public static FrameResult Failed(string error) =>
        new()
        {
            IsComplete = false,
            Error = error
        };
}

/// <summary>
/// Image format of decoded frame data.
/// </summary>
public enum ImageFormat
{
    /// <summary>
    /// JPEG compressed image.
    /// </summary>
    Jpeg,

    /// <summary>
    /// Raw RGB565 pixel data (2 bytes per pixel).
    /// </summary>
    Rgb565,

    /// <summary>
    /// Raw RGB888 pixel data (3 bytes per pixel).
    /// </summary>
    Rgb888
}

/// <summary>
/// Protocol statistics for monitoring and debugging.
/// </summary>
public record ProtocolStats
{
    /// <summary>
    /// Total HID reports received.
    /// </summary>
    public long PacketsReceived { get; init; }

    /// <summary>
    /// Total bytes received across all packets.
    /// </summary>
    public long BytesReceived { get; init; }

    /// <summary>
    /// Total complete frames decoded.
    /// </summary>
    public long FramesDecoded { get; init; }

    /// <summary>
    /// Packets with errors (wrong size, invalid header, etc.).
    /// </summary>
    public long ErrorPackets { get; init; }

    /// <summary>
    /// Frames that were incomplete/abandoned (e.g., reset mid-frame).
    /// </summary>
    public long DroppedFrames { get; init; }

    /// <summary>
    /// Time when last complete frame was received.
    /// </summary>
    public DateTime? LastFrameTime { get; init; }

    /// <summary>
    /// Size of last complete frame in bytes.
    /// </summary>
    public int LastFrameSize { get; init; }
}
