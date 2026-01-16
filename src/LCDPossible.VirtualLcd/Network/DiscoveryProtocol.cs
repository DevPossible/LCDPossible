using System.Text.Json;

namespace LCDPossible.VirtualLcd.Network;

/// <summary>
/// UDP-based discovery protocol for finding VirtualLCD instances on the network.
/// </summary>
/// <remarks>
/// Protocol:
/// - Discovery port: 5300 (broadcast)
/// - Device port: Varies per instance (default 5302)
/// - Discovery request: JSON with type "DISCOVER"
/// - Discovery response: JSON with device info
/// </remarks>
public static class VirtualDeviceDiscovery
{
    /// <summary>
    /// Default port for discovery broadcasts.
    /// </summary>
    public const int DiscoveryPort = 5300;

    /// <summary>
    /// Protocol version for compatibility checking.
    /// </summary>
    public const int ProtocolVersion = 1;

    /// <summary>
    /// Magic identifier for discovery messages.
    /// </summary>
    public const string MagicId = "LCDP";

    /// <summary>
    /// Default timeout for discovery operations.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(500);
}

/// <summary>
/// Discovery request message sent by clients looking for VirtualLCD instances.
/// </summary>
public sealed class DiscoveryRequest
{
    /// <summary>
    /// Magic identifier (must be "LCDP").
    /// </summary>
    public string Magic { get; init; } = VirtualDeviceDiscovery.MagicId;

    /// <summary>
    /// Protocol version.
    /// </summary>
    public int Version { get; init; } = VirtualDeviceDiscovery.ProtocolVersion;

    /// <summary>
    /// Message type (must be "DISCOVER").
    /// </summary>
    public string Type { get; init; } = "DISCOVER";

    /// <summary>
    /// Serialize to JSON bytes.
    /// </summary>
    public byte[] ToBytes() => JsonSerializer.SerializeToUtf8Bytes(this);

    /// <summary>
    /// Parse from JSON bytes.
    /// </summary>
    public static DiscoveryRequest? FromBytes(ReadOnlySpan<byte> data)
    {
        try
        {
            return JsonSerializer.Deserialize<DiscoveryRequest>(data);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Validate the request.
    /// </summary>
    public bool IsValid() =>
        Magic == VirtualDeviceDiscovery.MagicId &&
        Version == VirtualDeviceDiscovery.ProtocolVersion &&
        Type == "DISCOVER";
}

/// <summary>
/// Discovery response message sent by VirtualLCD instances.
/// </summary>
public sealed class DiscoveryResponse
{
    /// <summary>
    /// Magic identifier (must be "LCDP").
    /// </summary>
    public string Magic { get; init; } = VirtualDeviceDiscovery.MagicId;

    /// <summary>
    /// Protocol version.
    /// </summary>
    public int Version { get; init; } = VirtualDeviceDiscovery.ProtocolVersion;

    /// <summary>
    /// Message type (must be "ANNOUNCE").
    /// </summary>
    public string Type { get; init; } = "ANNOUNCE";

    /// <summary>
    /// Instance name (for display and sorting).
    /// </summary>
    public required string InstanceName { get; init; }

    /// <summary>
    /// UDP port where this instance receives HID packets.
    /// </summary>
    public required int DevicePort { get; init; }

    /// <summary>
    /// Protocol ID being simulated (e.g., "trofeo-vision").
    /// </summary>
    public required string ProtocolId { get; init; }

    /// <summary>
    /// Display width in pixels.
    /// </summary>
    public required int Width { get; init; }

    /// <summary>
    /// Display height in pixels.
    /// </summary>
    public required int Height { get; init; }

    /// <summary>
    /// USB Vendor ID being emulated.
    /// </summary>
    public required ushort VendorId { get; init; }

    /// <summary>
    /// USB Product ID being emulated.
    /// </summary>
    public required ushort ProductId { get; init; }

    /// <summary>
    /// HID report size.
    /// </summary>
    public required int HidReportSize { get; init; }

    /// <summary>
    /// Serialize to JSON bytes.
    /// </summary>
    public byte[] ToBytes() => JsonSerializer.SerializeToUtf8Bytes(this);

    /// <summary>
    /// Parse from JSON bytes.
    /// </summary>
    public static DiscoveryResponse? FromBytes(ReadOnlySpan<byte> data)
    {
        try
        {
            return JsonSerializer.Deserialize<DiscoveryResponse>(data);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Validate the response.
    /// </summary>
    public bool IsValid() =>
        Magic == VirtualDeviceDiscovery.MagicId &&
        Version == VirtualDeviceDiscovery.ProtocolVersion &&
        Type == "ANNOUNCE" &&
        !string.IsNullOrEmpty(InstanceName) &&
        DevicePort > 0 && DevicePort <= 65535;
}
