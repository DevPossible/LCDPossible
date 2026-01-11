using LCDPossible.Core.Usb;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Core.Devices.Drivers.Thermalright;

/// <summary>
/// Driver for Thermalright Trofeo Vision 360 ARGB LCD (1280x480).
/// VID: 0x0416, PID: 0x5302
/// </summary>
public sealed class TrofeoVisionDriver : ILcdDevice
{
    public const ushort VendorId = 0x0416;
    public const ushort ProductId = 0x5302;
    private const int ReportId = 0x00;
    private const int HidReportSize = 513; // Report ID (1 byte) + data (512 bytes)
    private const int DataPerPacket = 512;
    private const int HeaderSize = 20;

    // Protocol header bytes: DA DB DC DD
    private static readonly byte[] HeaderMagic = [0xDA, 0xDB, 0xDC, 0xDD];

    // Protocol command types
    private const byte CommandImage = 0x02;
    private const byte CompressionJpeg = 0x02;
    private const byte CompressionRgb565 = 0x01;

    private readonly HidDeviceInfo _hidDeviceInfo;
    private readonly IDeviceEnumerator _enumerator;
    private readonly ILogger<TrofeoVisionDriver>? _logger;
    private IHidDevice? _hidDevice;
    private bool _disposed;

    public TrofeoVisionDriver(HidDeviceInfo hidDeviceInfo, IDeviceEnumerator enumerator, ILogger<TrofeoVisionDriver>? logger = null)
    {
        _hidDeviceInfo = hidDeviceInfo ?? throw new ArgumentNullException(nameof(hidDeviceInfo));
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        _logger = logger;

        Info = new DeviceInfo(
            VendorId: VendorId,
            ProductId: ProductId,
            Name: "Thermalright Trofeo Vision 360 ARGB",
            Manufacturer: "Thermalright",
            DriverName: nameof(TrofeoVisionDriver),
            DevicePath: hidDeviceInfo.DevicePath,
            SerialNumber: hidDeviceInfo.SerialNumber);
    }

    public DeviceInfo Info { get; }

    public LcdCapabilities Capabilities { get; } = new(
        Width: 1280,
        Height: 480,
        SupportedFormats: [ColorFormat.Jpeg, ColorFormat.Rgb565],
        PreferredFormat: ColorFormat.Jpeg,
        MaxPacketSize: DataPerPacket,
        MaxFrameRate: 60,
        SupportsBrightness: true,
        SupportsOrientation: true);

    public bool IsConnected => _hidDevice?.IsOpen == true;

    public event EventHandler? Disconnected;

    private void OnDisconnected()
    {
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsConnected)
        {
            return;
        }

        try
        {
            _hidDevice = _enumerator.OpenDevice(_hidDeviceInfo);
            _logger?.LogInformation("Connected to Trofeo Vision: {DevicePath}", Info.DevicePath);

            // Send initialization sequence (if any)
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to Trofeo Vision: {DevicePath}", Info.DevicePath);
            throw;
        }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_hidDevice != null)
        {
            _hidDevice.Close();
            _hidDevice.Dispose();
            _hidDevice = null;
            _logger?.LogInformation("Disconnected from Trofeo Vision: {DevicePath}", Info.DevicePath);
            OnDisconnected();
        }

        return Task.CompletedTask;
    }

    public async Task SendFrameAsync(ReadOnlyMemory<byte> frameData, ColorFormat format, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsConnected || _hidDevice == null)
        {
            throw new InvalidOperationException("Device is not connected.");
        }

        if (!Capabilities.SupportsFormat(format))
        {
            throw new ArgumentException($"Unsupported color format: {format}", nameof(format));
        }

        var compressionType = format switch
        {
            ColorFormat.Jpeg => CompressionJpeg,
            ColorFormat.Rgb565 => CompressionRgb565,
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };

        // Build the complete packet with header + image data
        var packets = BuildImagePackets(frameData.Span, compressionType);

        foreach (var packet in packets)
        {
            await _hidDevice.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
        }

        _logger?.LogTrace("Sent frame: {ByteCount} bytes in {PacketCount} packets", frameData.Length, packets.Count);
    }

    public Task SetBrightnessAsync(byte brightness, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsConnected)
        {
            throw new InvalidOperationException("Device is not connected.");
        }

        // Brightness control packet format (based on protocol analysis)
        // Implementation depends on device-specific protocol
        _logger?.LogDebug("Setting brightness to {Brightness}", brightness);

        // TODO: Implement brightness control when protocol is fully understood
        return Task.CompletedTask;
    }

    public Task SetOrientationAsync(Orientation orientation, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsConnected)
        {
            throw new InvalidOperationException("Device is not connected.");
        }

        _logger?.LogDebug("Setting orientation to {Orientation}", orientation);

        // TODO: Implement orientation control when protocol is fully understood
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _hidDevice?.Dispose();
        _hidDevice = null;
        _disposed = true;
    }

    /// <summary>
    /// Builds the HID report packets following the protocol:
    /// Each packet: [Report ID 0x00] + [Data up to 512 bytes]
    /// First packet data: Header (20 bytes) + JPEG data start
    /// Header: DA DB DC DD 02 00 00 00 [width LE 2B] [height LE 2B] [compression] 00 00 00 [length LE 4B]
    /// </summary>
    private List<byte[]> BuildImagePackets(ReadOnlySpan<byte> imageData, byte compressionType)
    {
        var packets = new List<byte[]>();
        var totalLength = imageData.Length;

        // Build protocol header (20 bytes)
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
        header[8] = (byte)(Capabilities.Width & 0xFF);
        header[9] = (byte)(Capabilities.Width >> 8);

        // Height (little-endian): 480 = 0x01E0
        header[10] = (byte)(Capabilities.Height & 0xFF);
        header[11] = (byte)(Capabilities.Height >> 8);

        // Compression type: 0x02 for JPEG, 0x01 for RGB565
        header[12] = compressionType;

        // Reserved: 00 00 00
        header[13] = 0x00;
        header[14] = 0x00;
        header[15] = 0x00;

        // Image data length (little-endian, 4 bytes)
        header[16] = (byte)(totalLength & 0xFF);
        header[17] = (byte)((totalLength >> 8) & 0xFF);
        header[18] = (byte)((totalLength >> 16) & 0xFF);
        header[19] = (byte)((totalLength >> 24) & 0xFF);

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

            var chunkSize = Math.Min(fullData.Length - offset, DataPerPacket);
            Array.Copy(fullData, offset, packet, 1, chunkSize);
            packets.Add(packet);
            offset += chunkSize;
        }

        return packets;
    }

    /// <summary>
    /// Creates a driver instance from HID device info.
    /// </summary>
    public static TrofeoVisionDriver Create(HidDeviceInfo hidDeviceInfo, IDeviceEnumerator enumerator, ILoggerFactory? loggerFactory = null)
    {
        return new TrofeoVisionDriver(
            hidDeviceInfo,
            enumerator,
            loggerFactory?.CreateLogger<TrofeoVisionDriver>());
    }
}
