namespace LCDPossible.Core.Usb;

/// <summary>
/// Information about a discovered HID device.
/// </summary>
public sealed class HidDeviceInfo
{
    public required string DevicePath { get; init; }
    public required ushort VendorId { get; init; }
    public required ushort ProductId { get; init; }
    public string? Manufacturer { get; init; }
    public string? ProductName { get; init; }
    public string? SerialNumber { get; init; }
    public int MaxInputReportLength { get; init; }
    public int MaxOutputReportLength { get; init; }
    public int MaxFeatureReportLength { get; init; }

    public override string ToString() =>
        $"HID Device VID:0x{VendorId:X4} PID:0x{ProductId:X4} - {ProductName ?? "Unknown"}";
}
