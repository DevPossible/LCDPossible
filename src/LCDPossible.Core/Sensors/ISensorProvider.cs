namespace LCDPossible.Core.Sensors;

/// <summary>
/// Interface for providers that create sensors.
/// </summary>
public interface ISensorProvider
{
    /// <summary>
    /// Unique provider identifier (e.g., "lcdpossible.hardware").
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Initialize the provider.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Get all sensor types this provider can create.
    /// </summary>
    IReadOnlyList<SensorTypeInfo> GetSensorTypes();

    /// <summary>
    /// Create a sensor by ID.
    /// </summary>
    /// <param name="sensorId">The sensor ID to create.</param>
    /// <returns>The sensor, or null if not found.</returns>
    ISensor? CreateSensor(string sensorId);
}
