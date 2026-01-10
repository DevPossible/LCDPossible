using LCDPossible.Core.Devices.Drivers.Thermalright;
using LCDPossible.Core.Usb;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Core.Devices;

/// <summary>
/// Provides registration of all supported device drivers.
/// </summary>
public static class DriverRegistry
{
    /// <summary>
    /// Supported device definitions.
    /// </summary>
    public static readonly IReadOnlyList<SupportedDevice> SupportedDevices =
    [
        new(0x0416, 0x5302, "Thermalright Trofeo Vision 360 ARGB", nameof(TrofeoVisionDriver)),
        new(0x0416, 0x8001, "Thermalright PA120 Digital", nameof(PA120DigitalDriver)),
        // Future devices can be added here
        // new(0x0418, 0x5303, "Thermalright Secondary Controller", nameof(GenericLcdDriver)),
        // new(0x0418, 0x5304, "Thermalright Extended LCD", nameof(GenericLcdDriver)),
    ];

    /// <summary>
    /// Registers all known Thermalright device drivers with the device manager.
    /// </summary>
    /// <param name="deviceManager">The device manager to register drivers with.</param>
    /// <param name="enumerator">The device enumerator for creating drivers.</param>
    /// <param name="loggerFactory">Optional logger factory for driver logging.</param>
    public static void RegisterAllDrivers(DeviceManager deviceManager, IDeviceEnumerator enumerator, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(deviceManager);
        ArgumentNullException.ThrowIfNull(enumerator);

        // Trofeo Vision 360 ARGB (1280x480 LCD)
        deviceManager.RegisterDriver(
            TrofeoVisionDriver.VendorId,
            TrofeoVisionDriver.ProductId,
            hidInfo => TrofeoVisionDriver.Create(hidInfo, enumerator, loggerFactory));

        // PA120 Digital (Segment display)
        deviceManager.RegisterDriver(
            PA120DigitalDriver.VendorId,
            PA120DigitalDriver.ProductId,
            hidInfo => PA120DigitalDriver.Create(hidInfo, enumerator, loggerFactory));
    }

    /// <summary>
    /// Checks if a VID/PID combination is supported.
    /// </summary>
    public static bool IsSupported(ushort vendorId, ushort productId) =>
        SupportedDevices.Any(d => d.VendorId == vendorId && d.ProductId == productId);

    /// <summary>
    /// Gets the device name for a VID/PID combination.
    /// </summary>
    public static string? GetDeviceName(ushort vendorId, ushort productId) =>
        SupportedDevices.FirstOrDefault(d => d.VendorId == vendorId && d.ProductId == productId)?.DeviceName;
}

/// <summary>
/// Represents a supported device definition.
/// </summary>
/// <param name="VendorId">USB vendor ID.</param>
/// <param name="ProductId">USB product ID.</param>
/// <param name="DeviceName">Human-readable device name.</param>
/// <param name="DriverName">Name of the driver class.</param>
public record SupportedDevice(ushort VendorId, ushort ProductId, string DeviceName, string DriverName);
