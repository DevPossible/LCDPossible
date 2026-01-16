namespace LCDPossible.Core.Sensors;

/// <summary>
/// Base interface for all sensors.
/// </summary>
public interface ISensor : IDisposable
{
    /// <summary>
    /// Unique sensor identifier (e.g., "lcdpossible.cpu.usage").
    /// </summary>
    string SensorId { get; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Category for grouping (e.g., "cpu", "gpu", "memory").
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Unit of measurement (e.g., "%", "°C", "GB"). Null if dimensionless.
    /// </summary>
    string? Unit { get; }

    /// <summary>
    /// The CLR type of the sensor value.
    /// </summary>
    Type ValueType { get; }

    /// <summary>
    /// How the sensor updates its value.
    /// </summary>
    SensorUpdateMode UpdateMode { get; }

    /// <summary>
    /// Polling interval for Polling sensors.
    /// </summary>
    TimeSpan UpdateInterval { get; }

    /// <summary>
    /// Whether the sensor is available (hardware present, permissions granted).
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// When the value was last updated.
    /// </summary>
    DateTime LastUpdated { get; }

    /// <summary>
    /// Initialize the sensor (called once before first read).
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Get the current value (may return cached).
    /// </summary>
    Task<object?> GetValueAsync(CancellationToken ct = default);

    /// <summary>
    /// Force a refresh and return the new value.
    /// </summary>
    Task<object?> RefreshAsync(CancellationToken ct = default);
}

/// <summary>
/// Strongly-typed sensor interface.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public interface ISensor<T> : ISensor
{
    /// <summary>
    /// The cached value (may be stale).
    /// </summary>
    T? CachedValue { get; }

    /// <summary>
    /// Get the current value (may return cached).
    /// </summary>
    new Task<T?> GetValueAsync(CancellationToken ct = default);

    /// <summary>
    /// Force a refresh and return the new value.
    /// </summary>
    new Task<T?> RefreshAsync(CancellationToken ct = default);
}
