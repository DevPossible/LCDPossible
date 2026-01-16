using LCDPossible.Core.Devices;
using LCDPossible.Core.Usb;
using LCDPossible.Plugins.Thermalright.Drivers;
using LCDPossible.Plugins.Thermalright.Handlers;
using LCDPossible.Plugins.Thermalright.Protocol;
using LCDPossible.Core.Plugins;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Plugins.Thermalright;

/// <summary>
/// Device plugin for Thermalright LCD devices.
/// </summary>
public sealed class ThermalrightPlugin : IDevicePlugin
{
    private ILoggerFactory? _loggerFactory;
    private ILogger? _logger;
    private bool _disposed;

    public string PluginId => "lcdpossible.devices.thermalright";

    public string DisplayName => "Thermalright LCD Devices";

    public Version Version => new(1, 0, 0);

    public Version MinimumSdkVersion => new(1, 0, 0);

    public IReadOnlyList<SupportedDeviceInfo> SupportedDevices { get; } =
    [
        new()
        {
            VendorId = TrofeoVisionDriver.VendorId,
            ProductId = TrofeoVisionDriver.ProductId,
            DeviceName = "Thermalright Trofeo Vision 360 ARGB",
            ProtocolId = "thermalright-trofeo-vision",
            Width = TrofeoVisionProtocol.Width,
            Height = TrofeoVisionProtocol.Height
        }
        // Future: Add PA120DigitalDriver device here
    ];

    public IReadOnlyList<DeviceProtocolInfo> Protocols { get; } =
    [
        new()
        {
            ProtocolId = "thermalright-trofeo-vision",
            DisplayName = "Trofeo Vision Protocol",
            DefaultPort = 5302,
            Capabilities = new()
            {
                Width = TrofeoVisionProtocol.Width,
                Height = TrofeoVisionProtocol.Height,
                MaxPacketSize = TrofeoVisionProtocol.MaxPacketSize,
                MaxFrameRate = 60,
                SupportsBrightness = true,
                SupportsOrientation = true
            }
        }
    ];

    public Task InitializeAsync(IDevicePluginContext context, CancellationToken cancellationToken = default)
    {
        _loggerFactory = context.LoggerFactory;
        _logger = _loggerFactory?.CreateLogger<ThermalrightPlugin>();
        _logger?.LogInformation("Thermalright plugin initialized");
        return Task.CompletedTask;
    }

    public ILcdDevice? CreatePhysicalDevice(HidDeviceInfo hidInfo, IDeviceEnumerator enumerator)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (hidInfo.VendorId == TrofeoVisionDriver.VendorId &&
            hidInfo.ProductId == TrofeoVisionDriver.ProductId)
        {
            _logger?.LogDebug("Creating TrofeoVisionDriver for {DevicePath}", hidInfo.DevicePath);
            return new TrofeoVisionDriver(
                hidInfo,
                enumerator,
                _loggerFactory?.CreateLogger<TrofeoVisionDriver>());
        }

        // Future: Add PA120DigitalDriver check here

        _logger?.LogDebug(
            "No driver for VID:0x{VendorId:X4} PID:0x{ProductId:X4}",
            hidInfo.VendorId, hidInfo.ProductId);
        return null;
    }

    public ILcdDevice? CreateVirtualDevice(string protocolId, string endpoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (protocolId.Equals("thermalright-trofeo-vision", StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogDebug("Creating VirtualUdpDriver for {ProtocolId} at {Endpoint}", protocolId, endpoint);
            return new VirtualUdpDriver(
                protocolId,
                endpoint,
                new LcdCapabilities(
                    Width: TrofeoVisionProtocol.Width,
                    Height: TrofeoVisionProtocol.Height,
                    SupportedFormats: [ColorFormat.Jpeg, ColorFormat.Rgb565],
                    PreferredFormat: ColorFormat.Jpeg,
                    MaxPacketSize: TrofeoVisionProtocol.MaxPacketSize,
                    MaxFrameRate: 60,
                    SupportsBrightness: true,
                    SupportsOrientation: true),
                _loggerFactory?.CreateLogger<VirtualUdpDriver>());
        }

        _logger?.LogDebug("Unknown protocol: {ProtocolId}", protocolId);
        return null;
    }

    public IVirtualDeviceHandler? CreateSimulatorHandler(string protocolId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (protocolId.Equals("thermalright-trofeo-vision", StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogDebug("Creating TrofeoVisionHandler for {ProtocolId}", protocolId);
            return new TrofeoVisionHandler(_loggerFactory?.CreateLogger<TrofeoVisionHandler>());
        }

        _logger?.LogDebug("Unknown protocol: {ProtocolId}", protocolId);
        return null;
    }

    public string? GetProtocolId(ushort vendorId, ushort productId)
    {
        return SupportedDevices
            .FirstOrDefault(d => d.VendorId == vendorId && d.ProductId == productId)
            ?.ProtocolId;
    }

    public bool SupportsDevice(ushort vendorId, ushort productId) =>
        SupportedDevices.Any(d => d.VendorId == vendorId && d.ProductId == productId);

    public bool SupportsProtocol(string protocolId) =>
        Protocols.Any(p => p.ProtocolId.Equals(protocolId, StringComparison.OrdinalIgnoreCase));

    public void Dispose()
    {
        if (_disposed)
            return;

        _logger?.LogInformation("Thermalright plugin disposed");
        _disposed = true;
    }
}
