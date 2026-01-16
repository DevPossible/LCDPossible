namespace LCDPossible.Core.Sensors;

/// <summary>
/// Metadata about a sensor type.
/// </summary>
/// <param name="SensorId">Unique sensor identifier (e.g., "lcdpossible.cpu.usage").</param>
/// <param name="DisplayName">Human-readable name.</param>
/// <param name="Category">Category for grouping (e.g., "cpu", "gpu", "memory").</param>
/// <param name="Unit">Unit of measurement (e.g., "%", "°C", "GB").</param>
/// <param name="ValueType">The CLR type of the sensor value.</param>
/// <param name="UpdateMode">How the sensor updates.</param>
/// <param name="DefaultUpdateInterval">Default polling interval for Polling sensors.</param>
public record SensorTypeInfo(
    string SensorId,
    string DisplayName,
    string Category,
    string? Unit,
    Type ValueType,
    SensorUpdateMode UpdateMode,
    TimeSpan DefaultUpdateInterval = default)
{
    /// <summary>
    /// Creates a SensorTypeInfo with default update interval based on mode.
    /// </summary>
    public static SensorTypeInfo Create(
        string sensorId,
        string displayName,
        string category,
        string? unit,
        Type valueType,
        SensorUpdateMode updateMode)
    {
        var interval = updateMode == SensorUpdateMode.Polling
            ? TimeSpan.FromSeconds(1)
            : TimeSpan.Zero;

        return new SensorTypeInfo(sensorId, displayName, category, unit, valueType, updateMode, interval);
    }
}
