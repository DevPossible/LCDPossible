using LCDPossible.Core.Devices;
using LCDPossible.Core.Usb;
using LCDPossible.Plugins.Thermalright.Protocol;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Plugins.Thermalright.Drivers;

/// <summary>
/// Physical device driver for Thermalright Trofeo Vision 360 ARGB LCD.
/// Communicates via USB HID.
/// </summary>
public sealed class TrofeoVisionDriver : ILcdDevice
{
    public const ushort VendorId = 0x0416;
    public const ushort ProductId = 0x5302;

    private readonly HidDeviceInfo _hidDeviceInfo;
    private readonly IDeviceEnumerator _enumerator;
    private readonly ILogger? _logger;
    private IHidDevice? _hidDevice;
    private bool _disposed;

    public TrofeoVisionDriver(HidDeviceInfo hidDeviceInfo, IDeviceEnumerator enumerator, ILogger? logger = null)
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
        Width: TrofeoVisionProtocol.Width,
        Height: TrofeoVisionProtocol.Height,
        SupportedFormats: [ColorFormat.Jpeg, ColorFormat.Rgb565],
        PreferredFormat: ColorFormat.Jpeg,
        MaxPacketSize: TrofeoVisionProtocol.MaxPacketSize,
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
            ColorFormat.Jpeg => TrofeoVisionProtocol.CompressionJpeg,
            ColorFormat.Rgb565 => TrofeoVisionProtocol.CompressionRgb565,
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };

        // Build HID packets using protocol helper
        var packets = TrofeoVisionProtocol.BuildImagePackets(frameData.Span, compressionType);

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
}
