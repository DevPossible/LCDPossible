using System.Buffers.Binary;

namespace LCDPossible.VirtualLcd.Protocols;

/// <summary>
/// Protocol implementation for Thermalright Trofeo Vision 360 ARGB LCD.
///
/// Protocol format:
/// - HID Report: [Report ID 0x00] [Data: 512 bytes]
/// - First packet header (20 bytes):
///   [0-3]   Magic: DA DB DC DD
///   [4]     Command: 0x02 (image data)
///   [5-7]   Reserved: 00 00 00
///   [8-9]   Width (little-endian): 1280 = 0x0500
///   [10-11] Height (little-endian): 480 = 0x01E0
///   [12]    Compression: 0x02 (JPEG) or 0x01 (RGB565)
///   [13-15] Reserved: 00 00 00
///   [16-19] Image data length (little-endian, 4 bytes)
/// - Image data follows header, spanning multiple packets
/// </summary>
public sealed class TrofeoVisionProtocol : LcdProtocolBase
{
    // Protocol constants
    private static readonly byte[] HeaderMagic = [0xDA, 0xDB, 0xDC, 0xDD];
    private const byte CommandImage = 0x02;
    private const byte CompressionJpeg = 0x02;
    private const byte CompressionRgb565 = 0x01;
    private const int ProtocolHeaderSize = 20;

    // Current frame format (stored when parsing header)
    private ImageFormat _currentFrameFormat = ImageFormat.Jpeg;

    /// <inheritdoc />
    public override string ProtocolId => "trofeo-vision";

    /// <inheritdoc />
    public override string DisplayName => "Thermalright Trofeo Vision 360 ARGB";

    /// <inheritdoc />
    public override string Description => "1280x480 LCD with JPEG/RGB565 support (VID:0x0416 PID:0x5302)";

    /// <inheritdoc />
    public override int Width => 1280;

    /// <inheritdoc />
    public override int Height => 480;

    /// <inheritdoc />
    public override int HidReportSize => 513; // 1 byte report ID + 512 bytes data

    /// <inheritdoc />
    public override ushort VendorId => 0x0416; // Thermalright

    /// <inheritdoc />
    public override ushort ProductId => 0x5302; // Trofeo Vision

    /// <inheritdoc />
    protected override int HeaderSize => ProtocolHeaderSize;

    /// <inheritdoc />
    protected override bool IsFrameStart(ReadOnlySpan<byte> data)
    {
        // Check for magic bytes at start of data
        if (data.Length < 4)
            return false;

        return data[0] == HeaderMagic[0] &&
               data[1] == HeaderMagic[1] &&
               data[2] == HeaderMagic[2] &&
               data[3] == HeaderMagic[3];
    }

    /// <inheritdoc />
    protected override HeaderParseResult ParseHeader(ReadOnlySpan<byte> data)
    {
        if (data.Length < ProtocolHeaderSize)
        {
            return HeaderParseResult.Failure($"Header too short: expected {ProtocolHeaderSize}, got {data.Length}");
        }

        // Verify magic bytes
        if (!IsFrameStart(data))
        {
            return HeaderParseResult.Failure("Invalid magic bytes");
        }

        // Check command byte
        var command = data[4];
        if (command != CommandImage)
        {
            return HeaderParseResult.Failure($"Unknown command: 0x{command:X2}");
        }

        // Parse dimensions (for validation)
        var width = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(8, 2));
        var height = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(10, 2));

        // We could validate dimensions match expected, but allow flexibility
        // if (width != Width || height != Height) { ... }

        // Parse compression type
        var compression = data[12];
        var format = compression switch
        {
            CompressionJpeg => ImageFormat.Jpeg,
            CompressionRgb565 => ImageFormat.Rgb565,
            _ => ImageFormat.Jpeg // Default to JPEG for unknown
        };

        // Store for later retrieval
        _currentFrameFormat = format;

        // Parse image data length
        var imageLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16, 4));

        if (imageLength <= 0)
        {
            return HeaderParseResult.Failure($"Invalid image length: {imageLength}");
        }

        // Sanity check - max reasonable frame size (10MB for uncompressed RGB888)
        const int maxFrameSize = 10 * 1024 * 1024;
        if (imageLength > maxFrameSize)
        {
            return HeaderParseResult.Failure($"Image length exceeds maximum: {imageLength} > {maxFrameSize}");
        }

        return HeaderParseResult.Success(imageLength, format);
    }

    /// <inheritdoc />
    protected override ImageFormat GetCurrentFrameFormat() => _currentFrameFormat;

    /// <inheritdoc />
    public override void Reset()
    {
        base.Reset();
        _currentFrameFormat = ImageFormat.Jpeg;
    }
}
