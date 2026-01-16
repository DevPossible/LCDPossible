namespace LCDPossible.Core.Plugins;

/// <summary>
/// Describes a device protocol supported by a device plugin.
/// Used for virtual devices and simulators.
/// </summary>
public sealed record DeviceProtocolInfo
{
    /// <summary>
    /// Unique protocol identifier (e.g., "thermalright-trofeo-vision").
    /// </summary>
    public required string ProtocolId { get; init; }

    /// <summary>
    /// Human-readable protocol name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Device capabilities for this protocol.
    /// </summary>
    public required DeviceCapabilities Capabilities { get; init; }

    /// <summary>
    /// Default UDP port for virtual device communication.
    /// </summary>
    public int DefaultPort { get; init; } = 5302;

    public override string ToString() =>
        $"{DisplayName} ({ProtocolId})";
}
