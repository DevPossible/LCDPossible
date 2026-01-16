namespace LCDPossible.Core.Sensors;

/// <summary>
/// Defines how a sensor updates its value.
/// </summary>
public enum SensorUpdateMode
{
    /// <summary>
    /// The value is read once and cached forever (e.g., CPU name, hostname).
    /// </summary>
    Static,

    /// <summary>
    /// The value is refreshed at a regular interval (e.g., CPU usage, temperature).
    /// </summary>
    Polling,

    /// <summary>
    /// The value is updated by external events (e.g., device connect/disconnect).
    /// </summary>
    EventDriven
}
