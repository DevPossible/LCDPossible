namespace LCDPossible.Core.Plugins;

/// <summary>
/// Describes a physical device supported by a device plugin.
/// </summary>
public sealed record SupportedDeviceInfo
{
    /// <summary>
    /// USB Vendor ID.
    /// </summary>
    public required ushort VendorId { get; init; }

    /// <summary>
    /// USB Product ID.
    /// </summary>
    public required ushort ProductId { get; init; }

    /// <summary>
    /// Human-readable device name.
    /// </summary>
    public required string DeviceName { get; init; }

    /// <summary>
    /// Protocol identifier used for both physical and virtual devices.
    /// </summary>
    public required string ProtocolId { get; init; }

    /// <summary>
    /// Display width in pixels.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Display height in pixels.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Gets the VID:PID tuple.
    /// </summary>
    public (ushort VendorId, ushort ProductId) VidPid => (VendorId, ProductId);

    public override string ToString() =>
        $"{DeviceName} (VID:0x{VendorId:X4} PID:0x{ProductId:X4})";
}
