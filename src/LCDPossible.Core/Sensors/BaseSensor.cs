namespace LCDPossible.Core.Sensors;

/// <summary>
/// Abstract base class for sensors with built-in caching.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public abstract class BaseSensor<T> : ISensor<T>
{
    private readonly object _lock = new();
    private bool _isRefreshing;
    private bool _disposed;

    public abstract string SensorId { get; }
    public abstract string DisplayName { get; }
    public abstract string Category { get; }
    public virtual string? Unit => null;
    public Type ValueType => typeof(T);
    public abstract SensorUpdateMode UpdateMode { get; }
    public virtual TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);
    public virtual bool IsAvailable => true;
    public DateTime LastUpdated { get; protected set; } = DateTime.MinValue;

    public T? CachedValue { get; protected set; }

    public virtual Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public async Task<T?> GetValueAsync(CancellationToken ct = default)
    {
        // Static sensors only need to be read once
        if (UpdateMode == SensorUpdateMode.Static && LastUpdated != DateTime.MinValue)
        {
            return CachedValue;
        }

        // Check if cache is still valid
        if (DateTime.UtcNow - LastUpdated < UpdateInterval)
        {
            return CachedValue;
        }

        return await RefreshAsync(ct).ConfigureAwait(false);
    }

    public async Task<T?> RefreshAsync(CancellationToken ct = default)
    {
        // Thread-safe refresh
        lock (_lock)
        {
            if (_isRefreshing)
            {
                return CachedValue;
            }
            _isRefreshing = true;
        }

        try
        {
            CachedValue = await ReadValueAsync(ct).ConfigureAwait(false);
            LastUpdated = DateTime.UtcNow;
            return CachedValue;
        }
        finally
        {
            lock (_lock)
            {
                _isRefreshing = false;
            }
        }
    }

    /// <summary>
    /// Read the actual value from hardware/system.
    /// </summary>
    protected abstract Task<T?> ReadValueAsync(CancellationToken ct);

    async Task<object?> ISensor.GetValueAsync(CancellationToken ct) =>
        await GetValueAsync(ct).ConfigureAwait(false);

    async Task<object?> ISensor.RefreshAsync(CancellationToken ct) =>
        await RefreshAsync(ct).ConfigureAwait(false);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        OnDispose();
        GC.SuppressFinalize(this);
    }

    protected virtual void OnDispose() { }
}

/// <summary>
/// Sensor for static values that are read once.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public class StaticSensor<T> : BaseSensor<T>
{
    private readonly Func<CancellationToken, Task<T?>> _readFunc;

    public override string SensorId { get; }
    public override string DisplayName { get; }
    public override string Category { get; }
    public override string? Unit { get; }
    public override SensorUpdateMode UpdateMode => SensorUpdateMode.Static;
    public override TimeSpan UpdateInterval => TimeSpan.MaxValue;

    public StaticSensor(
        string sensorId,
        string displayName,
        string category,
        Func<CancellationToken, Task<T?>> readFunc,
        string? unit = null)
    {
        SensorId = sensorId;
        DisplayName = displayName;
        Category = category;
        Unit = unit;
        _readFunc = readFunc;
    }

    protected override Task<T?> ReadValueAsync(CancellationToken ct) => _readFunc(ct);
}

/// <summary>
/// Sensor for polling values at regular intervals.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public class PollingSensor<T> : BaseSensor<T>
{
    private readonly Func<CancellationToken, Task<T?>> _readFunc;
    private readonly TimeSpan _interval;

    public override string SensorId { get; }
    public override string DisplayName { get; }
    public override string Category { get; }
    public override string? Unit { get; }
    public override SensorUpdateMode UpdateMode => SensorUpdateMode.Polling;
    public override TimeSpan UpdateInterval => _interval;

    public PollingSensor(
        string sensorId,
        string displayName,
        string category,
        Func<CancellationToken, Task<T?>> readFunc,
        string? unit = null,
        TimeSpan? interval = null)
    {
        SensorId = sensorId;
        DisplayName = displayName;
        Category = category;
        Unit = unit;
        _readFunc = readFunc;
        _interval = interval ?? TimeSpan.FromSeconds(1);
    }

    protected override Task<T?> ReadValueAsync(CancellationToken ct) => _readFunc(ct);
}
