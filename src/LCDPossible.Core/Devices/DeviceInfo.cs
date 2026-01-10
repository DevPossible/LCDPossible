namespace LCDPossible.Core.Devices;

/// <summary>
/// Metadata about an LCD device.
/// </summary>
/// <param name="VendorId">USB vendor ID.</param>
/// <param name="ProductId">USB product ID.</param>
/// <param name="Name">Human-readable device name.</param>
/// <param name="Manufacturer">Device manufacturer.</param>
/// <param name="DriverName">Name of the driver handling this device.</param>
/// <param name="DevicePath">Platform-specific device path.</param>
/// <param name="SerialNumber">Device serial number, if available.</param>
public record DeviceInfo(
    ushort VendorId,
    ushort ProductId,
    string Name,
    string Manufacturer,
    string DriverName,
    string DevicePath,
    string? SerialNumber = null)
{
    /// <summary>
    /// Gets a unique identifier for this device based on VID:PID and path.
    /// </summary>
    public string UniqueId => $"{VendorId:X4}:{ProductId:X4}:{GetHashedPath()}";

    private string GetHashedPath() =>
        DevicePath.GetHashCode().ToString("X8");

    public override string ToString() =>
        $"{Name} ({Manufacturer}) - VID:0x{VendorId:X4} PID:0x{ProductId:X4}";
}
