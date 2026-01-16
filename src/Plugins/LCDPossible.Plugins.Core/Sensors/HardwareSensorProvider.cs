using LCDPossible.Core.Monitoring;
using LCDPossible.Core.Sensors;
using LCDPossible.Plugins.Core.Monitoring;

namespace LCDPossible.Plugins.Core.Sensors;

/// <summary>
/// Sensor provider that creates individual sensors from HardwareMonitorProvider.
/// </summary>
public sealed class HardwareSensorProvider : ISensorProvider, IDisposable
{
    private readonly HardwareMonitorProvider _hwProvider;
    private readonly bool _ownsProvider;
    private SystemMetrics? _cachedMetrics;
    private DateTime _lastMetricsUpdate = DateTime.MinValue;
    private readonly TimeSpan _metricsCacheInterval = TimeSpan.FromMilliseconds(500);
    private readonly object _metricsLock = new();
    private bool _disposed;

    public string ProviderId => "lcdpossible.hardware";
    public string DisplayName => "Hardware Monitor";

    public HardwareSensorProvider(HardwareMonitorProvider hwProvider)
    {
        _hwProvider = hwProvider;
        _ownsProvider = false;
    }

    public HardwareSensorProvider()
    {
        _hwProvider = new HardwareMonitorProvider();
        _ownsProvider = true;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _hwProvider.InitializeAsync(ct);
    }

    public IReadOnlyList<SensorTypeInfo> GetSensorTypes()
    {
        var sensors = new List<SensorTypeInfo>();

        // System sensors
        sensors.Add(new SensorTypeInfo("lcdpossible.system.hostname", "Hostname", "system", null, typeof(string), SensorUpdateMode.Static));
        sensors.Add(new SensorTypeInfo("lcdpossible.system.os.name", "OS Name", "system", null, typeof(string), SensorUpdateMode.Static));
        sensors.Add(new SensorTypeInfo("lcdpossible.system.uptime", "System Uptime", "system", null, typeof(TimeSpan), SensorUpdateMode.Polling, TimeSpan.FromSeconds(1)));

        // CPU sensors
        sensors.Add(new SensorTypeInfo("lcdpossible.cpu.name", "CPU Name", "cpu", null, typeof(string), SensorUpdateMode.Static));
        sensors.Add(new SensorTypeInfo("lcdpossible.cpu.cores.physical", "Physical Cores", "cpu", null, typeof(int), SensorUpdateMode.Static));
        sensors.Add(new SensorTypeInfo("lcdpossible.cpu.cores.logical", "Logical Cores", "cpu", null, typeof(int), SensorUpdateMode.Static));
        sensors.Add(new SensorTypeInfo("lcdpossible.cpu.usage", "CPU Usage", "cpu", "%", typeof(float), SensorUpdateMode.Polling, TimeSpan.FromSeconds(1)));
        sensors.Add(new SensorTypeInfo("lcdpossible.cpu.temperature", "CPU Temperature", "cpu", "°C", typeof(float), SensorUpdateMode.Polling, TimeSpan.FromSeconds(1)));
        sensors.Add(new SensorTypeInfo("lcdpossible.cpu.frequency", "CPU Frequency", "cpu", "MHz", typeof(float), SensorUpdateMode.Polling, TimeSpan.FromSeconds(1)));
        sensors.Add(new SensorTypeInfo("lcdpossible.cpu.power", "CPU Power", "cpu", "W", typeof(float), SensorUpdateMode.Polling, TimeSpan.FromSeconds(1)));

        // Memory sensors
        sensors.Add(new SensorTypeInfo("lcdpossible.memory.total", "Total Memory", "memory", "GB", typeof(float), SensorUpdateMode.Static));
        sensors.Add(new SensorTypeInfo("lcdpossible.memory.used", "Used Memory", "memory", "GB", typeof(float), SensorUpdateMode.Polling, TimeSpan.FromSeconds(1)));
        sensors.Add(new SensorTypeInfo("lcdpossible.memory.available", "Available Memory", "memory", "GB", typeof(float), SensorUpdateMode.Polling, TimeSpan.FromSeconds(1)));
        sensors.Add(new SensorTypeInfo("lcdpossible.memory.usage", "Memory Usage", "memory", "%", typeof(float), SensorUpdateMode.Polling, TimeSpan.FromSeconds(1)));

        // GPU sensors (for first GPU)
        sensors.Add(new SensorTypeInfo("lcdpossible.gpu.count", "GPU Count", "gpu", null, typeof(int), SensorUpdateMode.Static));
        sensors.Add(new SensorTypeInfo("lcdpossible.gpu.0.name", "GPU Name", "gpu", null, typeof(string), SensorUpdateMode.Static));
        sensors.Add(new SensorTypeInfo("lcdpossible.gpu.0.usage", "GPU Usage", "gpu", "%", typeof(float), SensorUpdateMode.Polling, TimeSpan.FromSeconds(1)));
        sensors.Add(new SensorTypeInfo("lcdpossible.gpu.0.temperature", "GPU Temperature", "gpu", "°C", typeof(float), SensorUpdateMode.Polling, TimeSpan.FromSeconds(1)));
        sensors.Add(new SensorTypeInfo("lcdpossible.gpu.0.memory.total", "GPU Memory Total", "gpu", "GB", typeof(float), SensorUpdateMode.Static));
        sensors.Add(new SensorTypeInfo("lcdpossible.gpu.0.memory.used", "GPU Memory Used", "gpu", "GB", typeof(float), SensorUpdateMode.Polling, TimeSpan.FromSeconds(1)));
        sensors.Add(new SensorTypeInfo("lcdpossible.gpu.0.memory.usage", "GPU Memory Usage", "gpu", "%", typeof(float), SensorUpdateMode.Polling, TimeSpan.FromSeconds(1)));

        return sensors;
    }

    public ISensor? CreateSensor(string sensorId)
    {
        var sensorTypes = GetSensorTypes();
        var typeInfo = sensorTypes.FirstOrDefault(s => s.SensorId.Equals(sensorId, StringComparison.OrdinalIgnoreCase));

        if (typeInfo == null)
            return null;

        return new HardwareSensor(typeInfo, this);
    }

    internal async Task<SystemMetrics?> GetCachedMetricsAsync(CancellationToken ct)
    {
        lock (_metricsLock)
        {
            if (_cachedMetrics != null && DateTime.UtcNow - _lastMetricsUpdate < _metricsCacheInterval)
            {
                return _cachedMetrics;
            }
        }

        var metrics = await _hwProvider.GetMetricsAsync(ct);

        lock (_metricsLock)
        {
            _cachedMetrics = metrics;
            _lastMetricsUpdate = DateTime.UtcNow;
        }

        return metrics;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_ownsProvider)
        {
            _hwProvider.Dispose();
        }
    }
}

/// <summary>
/// Individual hardware sensor that reads from cached metrics.
/// </summary>
internal sealed class HardwareSensor : ISensor
{
    private readonly SensorTypeInfo _typeInfo;
    private readonly HardwareSensorProvider _provider;
    private object? _cachedValue;
    private DateTime _lastUpdated = DateTime.MinValue;

    public string SensorId => _typeInfo.SensorId;
    public string DisplayName => _typeInfo.DisplayName;
    public string Category => _typeInfo.Category;
    public string? Unit => _typeInfo.Unit;
    public Type ValueType => _typeInfo.ValueType;
    public SensorUpdateMode UpdateMode => _typeInfo.UpdateMode;
    public TimeSpan UpdateInterval => _typeInfo.DefaultUpdateInterval;
    public bool IsAvailable => true;
    public DateTime LastUpdated => _lastUpdated;

    public HardwareSensor(SensorTypeInfo typeInfo, HardwareSensorProvider provider)
    {
        _typeInfo = typeInfo;
        _provider = provider;
    }

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public async Task<object?> GetValueAsync(CancellationToken ct = default)
    {
        // Return cached value if still valid
        if (_cachedValue != null && UpdateMode == SensorUpdateMode.Static)
            return _cachedValue;

        if (_cachedValue != null && DateTime.UtcNow - _lastUpdated < UpdateInterval)
            return _cachedValue;

        return await RefreshAsync(ct);
    }

    public async Task<object?> RefreshAsync(CancellationToken ct = default)
    {
        var metrics = await _provider.GetCachedMetricsAsync(ct);
        _cachedValue = ExtractValue(metrics);
        _lastUpdated = DateTime.UtcNow;
        return _cachedValue;
    }

    private object? ExtractValue(SystemMetrics? metrics)
    {
        if (metrics == null) return null;

        return SensorId switch
        {
            // System
            "lcdpossible.system.hostname" => Environment.MachineName,
            "lcdpossible.system.os.name" => $"{Environment.OSVersion.Platform} {Environment.OSVersion.Version}",
            "lcdpossible.system.uptime" => TimeSpan.FromMilliseconds(Environment.TickCount64),

            // CPU
            "lcdpossible.cpu.name" => metrics.Cpu?.Name,
            "lcdpossible.cpu.cores.physical" => metrics.Cpu?.CoreUsages?.Count ?? Environment.ProcessorCount,
            "lcdpossible.cpu.cores.logical" => Environment.ProcessorCount,
            "lcdpossible.cpu.usage" => metrics.Cpu?.UsagePercent,
            "lcdpossible.cpu.temperature" => metrics.Cpu?.TemperatureCelsius,
            "lcdpossible.cpu.frequency" => metrics.Cpu?.FrequencyMhz,
            "lcdpossible.cpu.power" => metrics.Cpu?.PowerWatts,

            // Memory
            "lcdpossible.memory.total" => metrics.Memory?.TotalGb,
            "lcdpossible.memory.used" => metrics.Memory?.UsedGb,
            "lcdpossible.memory.available" => metrics.Memory?.AvailableGb,
            "lcdpossible.memory.usage" => metrics.Memory?.UsagePercent,

            // GPU (single GPU in SystemMetrics)
            "lcdpossible.gpu.count" => metrics.Gpu != null ? 1 : 0,
            "lcdpossible.gpu.0.name" => metrics.Gpu?.Name,
            "lcdpossible.gpu.0.usage" => metrics.Gpu?.UsagePercent,
            "lcdpossible.gpu.0.temperature" => metrics.Gpu?.TemperatureCelsius,
            "lcdpossible.gpu.0.memory.total" => metrics.Gpu?.MemoryTotalMb / 1024f, // Convert MB to GB
            "lcdpossible.gpu.0.memory.used" => metrics.Gpu?.MemoryUsedMb / 1024f,  // Convert MB to GB
            "lcdpossible.gpu.0.memory.usage" => metrics.Gpu?.MemoryUsagePercent,

            _ => null
        };
    }

    public void Dispose() { }
}
