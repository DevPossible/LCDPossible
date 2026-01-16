namespace LCDPossible.Plugins.Thermalright.Protocol;

/// <summary>
/// Protocol implementation for Thermalright Trofeo Vision LCD devices.
/// Handles packet encoding and decoding.
/// </summary>
public static class TrofeoVisionProtocol
{
    /// <summary>
    /// Protocol constants.
    /// </summary>
    public const int Width = 1280;
    public const int Height = 480;
    public const int MaxPacketSize = 512;
    public const int HidReportSize = 513; // Report ID (1 byte) + data (512 bytes)
    public const int HeaderSize = 20;
    public const byte ReportId = 0x00;

    /// <summary>
    /// Protocol header magic bytes: DA DB DC DD
    /// </summary>
    public static readonly byte[] HeaderMagic = [0xDA, 0xDB, 0xDC, 0xDD];

    /// <summary>
    /// Command types.
    /// </summary>
    public const byte CommandImage = 0x02;

    /// <summary>
    /// Compression types.
    /// </summary>
    public const byte CompressionJpeg = 0x02;
    public const byte CompressionRgb565 = 0x01;

    /// <summary>
    /// Builds HID report packets for image data.
    /// Each packet: [Report ID 0x00] + [Data up to 512 bytes]
    /// First packet data: Header (20 bytes) + image data start
    /// </summary>
    /// <param name="imageData">The encoded image data (JPEG or RGB565).</param>
    /// <param name="compressionType">Compression type (0x02 for JPEG, 0x01 for RGB565).</param>
    /// <returns>List of HID report packets ready to send.</returns>
    public static List<byte[]> BuildImagePackets(ReadOnlySpan<byte> imageData, byte compressionType)
    {
        var packets = new List<byte[]>();
        var totalLength = imageData.Length;

        // Build protocol header (20 bytes)
        var header = BuildHeader(totalLength, compressionType);

        // Combine header + image data into full payload
        var fullData = new byte[HeaderSize + imageData.Length];
        header.CopyTo(fullData, 0);
        imageData.CopyTo(fullData.AsSpan(HeaderSize));

        // Split into HID reports (513 bytes each: 1 byte report ID + 512 bytes data)
        var offset = 0;
        while (offset < fullData.Length)
        {
            var packet = new byte[HidReportSize];
            packet[0] = ReportId; // HID Report ID

            var chunkSize = Math.Min(fullData.Length - offset, MaxPacketSize);
            Array.Copy(fullData, offset, packet, 1, chunkSize);
            packets.Add(packet);
            offset += chunkSize;
        }

        return packets;
    }

    /// <summary>
    /// Builds HID report packets without Report ID prefix (for UDP transmission).
    /// </summary>
    public static List<byte[]> BuildDataPackets(ReadOnlySpan<byte> imageData, byte compressionType)
    {
        var packets = new List<byte[]>();
        var totalLength = imageData.Length;

        // Build protocol header (20 bytes)
        var header = BuildHeader(totalLength, compressionType);

        // Combine header + image data into full payload
        var fullData = new byte[HeaderSize + imageData.Length];
        header.CopyTo(fullData, 0);
        imageData.CopyTo(fullData.AsSpan(HeaderSize));

        // Split into data packets (512 bytes each, no report ID)
        var offset = 0;
        while (offset < fullData.Length)
        {
            var chunkSize = Math.Min(fullData.Length - offset, MaxPacketSize);
            var packet = new byte[chunkSize];
            Array.Copy(fullData, offset, packet, 0, chunkSize);
            packets.Add(packet);
            offset += chunkSize;
        }

        return packets;
    }

    /// <summary>
    /// Builds the 20-byte protocol header.
    /// Format: DA DB DC DD 02 00 00 00 [width LE 2B] [height LE 2B] [compression] 00 00 00 [length LE 4B]
    /// </summary>
    private static byte[] BuildHeader(int imageLength, byte compressionType)
    {
        var header = new byte[HeaderSize];

        // Magic bytes: DA DB DC DD
        HeaderMagic.CopyTo(header, 0);

        // Command: 0x02 (image data)
        header[4] = CommandImage;

        // Reserved: 00 00 00
        header[5] = 0x00;
        header[6] = 0x00;
        header[7] = 0x00;

        // Width (little-endian): 1280 = 0x0500
        header[8] = (byte)(Width & 0xFF);
        header[9] = (byte)(Width >> 8);

        // Height (little-endian): 480 = 0x01E0
        header[10] = (byte)(Height & 0xFF);
        header[11] = (byte)(Height >> 8);

        // Compression type
        header[12] = compressionType;

        // Reserved: 00 00 00
        header[13] = 0x00;
        header[14] = 0x00;
        header[15] = 0x00;

        // Image data length (little-endian, 4 bytes)
        header[16] = (byte)(imageLength & 0xFF);
        header[17] = (byte)((imageLength >> 8) & 0xFF);
        header[18] = (byte)((imageLength >> 16) & 0xFF);
        header[19] = (byte)((imageLength >> 24) & 0xFF);

        return header;
    }

    /// <summary>
    /// Checks if a packet starts with the protocol header magic.
    /// </summary>
    public static bool IsHeaderPacket(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderMagic.Length)
            return false;

        return data[0] == HeaderMagic[0] &&
               data[1] == HeaderMagic[1] &&
               data[2] == HeaderMagic[2] &&
               data[3] == HeaderMagic[3];
    }

    /// <summary>
    /// Parses header information from a header packet.
    /// </summary>
    /// <param name="data">The header packet data (at least 20 bytes).</param>
    /// <returns>Parsed header info, or null if invalid.</returns>
    public static HeaderInfo? ParseHeader(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize || !IsHeaderPacket(data))
            return null;

        var command = data[4];
        var width = data[8] | (data[9] << 8);
        var height = data[10] | (data[11] << 8);
        var compression = data[12];
        var length = data[16] | (data[17] << 8) | (data[18] << 16) | (data[19] << 24);

        return new HeaderInfo
        {
            Command = command,
            Width = width,
            Height = height,
            Compression = compression,
            DataLength = length
        };
    }

    /// <summary>
    /// Parsed protocol header information.
    /// </summary>
    public sealed class HeaderInfo
    {
        public byte Command { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public byte Compression { get; init; }
        public int DataLength { get; init; }

        public bool IsJpeg => Compression == CompressionJpeg;
        public bool IsRgb565 => Compression == CompressionRgb565;
    }
}
