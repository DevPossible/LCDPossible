namespace LCDPossible.Core.Usb;

/// <summary>
/// Interface for enumerating HID devices across platforms.
/// </summary>
public interface IDeviceEnumerator : IDisposable
{
    /// <summary>
    /// Enumerates all HID devices.
    /// </summary>
    /// <returns>Collection of discovered HID devices.</returns>
    IEnumerable<HidDeviceInfo> EnumerateDevices();

    /// <summary>
    /// Enumerates HID devices with a specific vendor ID.
    /// </summary>
    /// <param name="vendorId">The vendor ID to filter by.</param>
    /// <returns>Collection of discovered HID devices.</returns>
    IEnumerable<HidDeviceInfo> EnumerateDevices(ushort vendorId);

    /// <summary>
    /// Enumerates HID devices with specific vendor and product IDs.
    /// </summary>
    /// <param name="vendorId">The vendor ID to filter by.</param>
    /// <param name="productId">The product ID to filter by.</param>
    /// <returns>Collection of discovered HID devices.</returns>
    IEnumerable<HidDeviceInfo> EnumerateDevices(ushort vendorId, ushort productId);

    /// <summary>
    /// Opens a HID device for communication.
    /// </summary>
    /// <param name="deviceInfo">The device to open.</param>
    /// <returns>The opened HID device.</returns>
    /// <exception cref="IOException">Failed to open device.</exception>
    IHidDevice OpenDevice(HidDeviceInfo deviceInfo);

    /// <summary>
    /// Starts monitoring for device arrival/removal events.
    /// </summary>
    void StartMonitoring();

    /// <summary>
    /// Stops monitoring for device events.
    /// </summary>
    void StopMonitoring();

    /// <summary>
    /// Event raised when a HID device is connected.
    /// </summary>
    event EventHandler<DeviceEventArgs>? DeviceArrived;

    /// <summary>
    /// Event raised when a HID device is disconnected.
    /// </summary>
    event EventHandler<DeviceEventArgs>? DeviceRemoved;
}
