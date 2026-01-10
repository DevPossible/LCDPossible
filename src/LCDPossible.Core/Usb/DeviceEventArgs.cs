namespace LCDPossible.Core.Usb;

/// <summary>
/// Event arguments for device arrival/removal events.
/// </summary>
public sealed class DeviceEventArgs : EventArgs
{
    public required HidDeviceInfo Device { get; init; }
}
