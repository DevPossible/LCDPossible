namespace LCDPossible.Core.Sensors;

/// <summary>
/// Central registry for sensors and providers.
/// </summary>
public interface ISensorRegistry : IDisposable
{
    /// <summary>
    /// Register a sensor provider.
    /// </summary>
    void RegisterProvider(ISensorProvider provider);

    /// <summary>
    /// Unregister a sensor provider.
    /// </summary>
    void UnregisterProvider(string providerId);

    /// <summary>
    /// Get all registered providers.
    /// </summary>
    IReadOnlyList<ISensorProvider> GetProviders();

    /// <summary>
    /// Get all available sensor types across all providers.
    /// </summary>
    IReadOnlyList<SensorTypeInfo> GetAllSensorTypes();

    /// <summary>
    /// Get sensor types by category.
    /// </summary>
    IReadOnlyList<SensorTypeInfo> GetSensorTypesByCategory(string category);

    /// <summary>
    /// Get or create a sensor by ID.
    /// </summary>
    ISensor? GetSensor(string sensorId);

    /// <summary>
    /// Read a sensor value asynchronously.
    /// </summary>
    Task<T?> ReadAsync<T>(string sensorId, CancellationToken ct = default);

    /// <summary>
    /// Read a sensor's cached value synchronously.
    /// </summary>
    T? ReadCached<T>(string sensorId);
}
