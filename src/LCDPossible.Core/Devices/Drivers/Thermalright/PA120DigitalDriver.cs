using LCDPossible.Core.Usb;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Core.Devices.Drivers.Thermalright;

/// <summary>
/// Driver for Thermalright PA120 Digital segment display.
/// VID: 0x0416, PID: 0x8001
/// This is a simpler digit-based display (not a full LCD).
/// </summary>
public sealed class PA120DigitalDriver : ILcdDevice
{
    public const ushort VendorId = 0x0416;
    public const ushort ProductId = 0x8001;
    private const int PacketSize = 64;

    private readonly HidDeviceInfo _hidDeviceInfo;
    private readonly IDeviceEnumerator _enumerator;
    private readonly ILogger<PA120DigitalDriver>? _logger;
    private IHidDevice? _hidDevice;
    private bool _disposed;

    public PA120DigitalDriver(HidDeviceInfo hidDeviceInfo, IDeviceEnumerator enumerator, ILogger<PA120DigitalDriver>? logger = null)
    {
        _hidDeviceInfo = hidDeviceInfo ?? throw new ArgumentNullException(nameof(hidDeviceInfo));
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        _logger = logger;

        Info = new DeviceInfo(
            VendorId: VendorId,
            ProductId: ProductId,
            Name: "Thermalright PA120 Digital",
            Manufacturer: "Thermalright",
            DriverName: nameof(PA120DigitalDriver),
            DevicePath: hidDeviceInfo.DevicePath,
            SerialNumber: hidDeviceInfo.SerialNumber);
    }

    public DeviceInfo Info { get; }

    // PA120 is a segment display, not a pixel-based LCD
    public LcdCapabilities Capabilities { get; } = new(
        Width: 0,
        Height: 0,
        SupportedFormats: [],  // Uses custom data format for digits
        PreferredFormat: ColorFormat.Rgb565, // Not actually used
        MaxPacketSize: PacketSize,
        MaxFrameRate: 10,
        SupportsBrightness: false,
        SupportsOrientation: false);

    public bool IsConnected => _hidDevice?.IsOpen == true;

    public event EventHandler? Disconnected;

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
            _logger?.LogInformation("Connected to PA120 Digital: {DevicePath}", Info.DevicePath);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to PA120 Digital: {DevicePath}", Info.DevicePath);
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
            _logger?.LogInformation("Disconnected from PA120 Digital: {DevicePath}", Info.DevicePath);
        }

        return Task.CompletedTask;
    }

    public Task SendFrameAsync(ReadOnlyMemory<byte> frameData, ColorFormat format, CancellationToken cancellationToken = default)
    {
        // PA120 Digital doesn't support standard frame data
        // Use SendTemperatureAsync instead
        throw new NotSupportedException("PA120 Digital uses SendTemperatureAsync for data display.");
    }

    /// <summary>
    /// Sends CPU and GPU temperature values to the display.
    /// </summary>
    /// <param name="cpuTemp">CPU temperature in degrees Celsius.</param>
    /// <param name="gpuTemp">GPU temperature in degrees Celsius.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendTemperatureAsync(int cpuTemp, int gpuTemp, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsConnected || _hidDevice == null)
        {
            throw new InvalidOperationException("Device is not connected.");
        }

        // Protocol based on digital_thermal_right_lcd project
        // Header: DC DD, then temperature data
        var packet = new byte[PacketSize];
        packet[0] = 0xDC;
        packet[1] = 0xDD;
        packet[2] = (byte)(cpuTemp & 0xFF);
        packet[3] = (byte)(gpuTemp & 0xFF);

        await _hidDevice.WriteAsync(packet, cancellationToken).ConfigureAwait(false);

        _logger?.LogTrace("Sent temperatures - CPU: {CpuTemp}C, GPU: {GpuTemp}C", cpuTemp, gpuTemp);
    }

    public Task SetBrightnessAsync(byte brightness, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("PA120 Digital does not support brightness control.");
    }

    public Task SetOrientationAsync(Orientation orientation, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("PA120 Digital does not support orientation control.");
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
    /// Creates a driver instance from HID device info.
    /// </summary>
    public static PA120DigitalDriver Create(HidDeviceInfo hidDeviceInfo, IDeviceEnumerator enumerator, ILoggerFactory? loggerFactory = null)
    {
        return new PA120DigitalDriver(
            hidDeviceInfo,
            enumerator,
            loggerFactory?.CreateLogger<PA120DigitalDriver>());
    }
}
