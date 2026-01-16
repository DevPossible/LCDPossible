using System.Collections.Concurrent;

namespace LCDPossible.Core.Sensors;

/// <summary>
/// Default implementation of ISensorRegistry.
/// </summary>
public sealed class SensorRegistry : ISensorRegistry
{
    private readonly ConcurrentDictionary<string, ISensorProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ISensor> _sensors = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public void RegisterProvider(ISensorProvider provider)
    {
        _providers[provider.ProviderId] = provider;
    }

    public void UnregisterProvider(string providerId)
    {
        if (_providers.TryRemove(providerId, out _))
        {
            // Remove sensors from this provider
            var prefix = providerId.Split('.').FirstOrDefault() ?? providerId;
            var toRemove = _sensors.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var key in toRemove)
            {
                if (_sensors.TryRemove(key, out var sensor))
                {
                    sensor.Dispose();
                }
            }
        }
    }

    public IReadOnlyList<ISensorProvider> GetProviders() =>
        _providers.Values.ToList();

    public IReadOnlyList<SensorTypeInfo> GetAllSensorTypes() =>
        _providers.Values.SelectMany(p => p.GetSensorTypes()).ToList();

    public IReadOnlyList<SensorTypeInfo> GetSensorTypesByCategory(string category) =>
        GetAllSensorTypes().Where(s => s.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();

    public ISensor? GetSensor(string sensorId)
    {
        // Return cached sensor if exists
        if (_sensors.TryGetValue(sensorId, out var existing))
        {
            return existing;
        }

        // Find provider that can create this sensor
        foreach (var provider in _providers.Values)
        {
            var sensor = provider.CreateSensor(sensorId);
            if (sensor != null)
            {
                // Cache and return
                _sensors[sensorId] = sensor;
                return sensor;
            }
        }

        return null;
    }

    public async Task<T?> ReadAsync<T>(string sensorId, CancellationToken ct = default)
    {
        var sensor = GetSensor(sensorId);
        if (sensor == null)
        {
            return default;
        }

        await sensor.InitializeAsync(ct);
        var value = await sensor.GetValueAsync(ct);

        if (value is T typedValue)
        {
            return typedValue;
        }

        // Try conversion
        if (value != null)
        {
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default;
            }
        }

        return default;
    }

    public T? ReadCached<T>(string sensorId)
    {
        if (!_sensors.TryGetValue(sensorId, out var sensor))
        {
            return default;
        }

        if (sensor is ISensor<T> typedSensor)
        {
            return typedSensor.CachedValue;
        }

        return default;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var sensor in _sensors.Values)
        {
            sensor.Dispose();
        }
        _sensors.Clear();
        _providers.Clear();
    }
}
