using LCDPossible.Core.Devices;
using LCDPossible.Core.Usb;

namespace LCDPossible.Core.Plugins;

/// <summary>
/// Interface for device driver plugins.
/// Implement this to provide support for LCD devices.
/// </summary>
public interface IDevicePlugin : IDisposable
{
    /// <summary>
    /// Unique plugin identifier (e.g., "lcdpossible.devices.thermalright").
    /// Use reverse-domain notation to avoid conflicts.
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// Human-readable plugin name displayed in UI and logs.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Plugin version.
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// Minimum SDK version this plugin requires.
    /// </summary>
    Version MinimumSdkVersion { get; }

    /// <summary>
    /// Physical devices supported by this plugin.
    /// Keyed by VID:PID tuple.
    /// </summary>
    IReadOnlyList<SupportedDeviceInfo> SupportedDevices { get; }

    /// <summary>
    /// Protocols supported by this plugin (for virtual devices and simulators).
    /// </summary>
    IReadOnlyList<DeviceProtocolInfo> Protocols { get; }

    /// <summary>
    /// Called once when the plugin is loaded.
    /// Use this to initialize any shared resources.
    /// </summary>
    /// <param name="context">Plugin context with services and configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InitializeAsync(IDevicePluginContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a physical device driver for a USB HID device.
    /// </summary>
    /// <param name="hidInfo">Information about the HID device.</param>
    /// <param name="enumerator">Device enumerator for opening the device.</param>
    /// <returns>The device driver, or null if not supported.</returns>
    ILcdDevice? CreatePhysicalDevice(HidDeviceInfo hidInfo, IDeviceEnumerator enumerator);

    /// <summary>
    /// Creates a virtual device driver that sends protocol data over UDP.
    /// </summary>
    /// <param name="protocolId">The protocol identifier.</param>
    /// <param name="endpoint">The UDP endpoint (host:port) to send to.</param>
    /// <returns>The virtual device driver, or null if protocol not supported.</returns>
    ILcdDevice? CreateVirtualDevice(string protocolId, string endpoint);

    /// <summary>
    /// Creates a simulator handler that receives and decodes protocol data.
    /// Used by the VirtualLcd simulator application.
    /// </summary>
    /// <param name="protocolId">The protocol identifier.</param>
    /// <returns>The protocol handler, or null if protocol not supported.</returns>
    IVirtualDeviceHandler? CreateSimulatorHandler(string protocolId);

    /// <summary>
    /// Gets the protocol ID for a specific device by VID:PID.
    /// </summary>
    /// <param name="vendorId">USB Vendor ID.</param>
    /// <param name="productId">USB Product ID.</param>
    /// <returns>The protocol ID, or null if device not supported.</returns>
    string? GetProtocolId(ushort vendorId, ushort productId);

    /// <summary>
    /// Checks if this plugin supports a device by VID:PID.
    /// </summary>
    bool SupportsDevice(ushort vendorId, ushort productId);

    /// <summary>
    /// Checks if this plugin supports a protocol by ID.
    /// </summary>
    bool SupportsProtocol(string protocolId);
}
