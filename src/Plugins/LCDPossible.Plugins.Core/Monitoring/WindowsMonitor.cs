using LCDPossible.Core.Monitoring;
using LibreHardwareMonitor.Hardware;

namespace LCDPossible.Plugins.Core.Monitoring;

/// <summary>
/// Windows hardware monitor using LibreHardwareMonitor.
/// Provides full hardware data including CPU, GPU, RAM, temps, fans, and power.
/// </summary>
internal sealed class WindowsMonitor : IPlatformMonitor
{
    private Computer? _computer;

    public string PlatformName => "Windows/LibreHardwareMonitor";
    public bool IsAvailable { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsStorageEnabled = true,
                IsNetworkEnabled = true
            };
            _computer.Open();
            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
        }

        return Task.CompletedTask;
    }

    public Task<SystemMetrics?> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable || _computer == null)
        {
            return Task.FromResult<SystemMetrics?>(null);
        }

        var metrics = new SystemMetrics
        {
            Cpu = new CpuMetrics { Name = "Unknown", UsagePercent = 0 },
            Gpu = new GpuMetrics { Name = "Unknown", UsagePercent = 0 },
            Memory = new MemoryMetrics(),
            Timestamp = DateTime.UtcNow
        };

        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();

            switch (hardware.HardwareType)
            {
                case HardwareType.Cpu:
                    UpdateCpuMetrics(hardware, metrics);
                    break;

                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    UpdateGpuMetrics(hardware, metrics);
                    break;

                case HardwareType.Memory:
                    UpdateMemoryMetrics(hardware, metrics);
                    break;

                case HardwareType.Storage:
                    UpdateStorageMetrics(hardware, metrics);
                    break;

                case HardwareType.Network:
                    UpdateNetworkMetrics(hardware, metrics);
                    break;
            }
        }

        return Task.FromResult<SystemMetrics?>(metrics);
    }

    private static void UpdateCpuMetrics(IHardware hardware, SystemMetrics metrics)
    {
        metrics.Cpu!.Name = hardware.Name;
        var coreUsages = new List<float>();

        foreach (var sensor in hardware.Sensors)
        {
            switch (sensor.SensorType)
            {
                case SensorType.Load when sensor.Name.Contains("Total"):
                    metrics.Cpu.UsagePercent = sensor.Value ?? 0;
                    break;
                case SensorType.Load when sensor.Name.StartsWith("CPU Core"):
                    coreUsages.Add(sensor.Value ?? 0);
                    break;
                case SensorType.Temperature when sensor.Name.Contains("Package") || sensor.Name.Contains("Average"):
                    metrics.Cpu.TemperatureCelsius = sensor.Value;
                    break;
                case SensorType.Clock when sensor.Name.Contains("Core"):
                    // Take the first core clock as representative
                    metrics.Cpu.FrequencyMhz ??= sensor.Value;
                    break;
                case SensorType.Power when sensor.Name.Contains("Package"):
                    metrics.Cpu.PowerWatts = sensor.Value;
                    break;
            }
        }

        if (coreUsages.Count > 0)
        {
            metrics.Cpu.CoreUsages = coreUsages.OrderBy(x => x).ToList();
        }
    }

    private static void UpdateGpuMetrics(IHardware hardware, SystemMetrics metrics)
    {
        // Prefer discrete GPUs over integrated
        if (metrics.Gpu!.Name != "Unknown" && hardware.HardwareType == HardwareType.GpuIntel)
        {
            return;
        }

        metrics.Gpu.Name = hardware.Name;

        foreach (var sensor in hardware.Sensors)
        {
            switch (sensor.SensorType)
            {
                case SensorType.Load when sensor.Name.Contains("GPU Core") || sensor.Name == "GPU Core":
                    metrics.Gpu.UsagePercent = sensor.Value ?? 0;
                    break;
                case SensorType.Load when sensor.Name.Contains("GPU Memory"):
                    metrics.Gpu.MemoryUsagePercent = sensor.Value;
                    break;
                case SensorType.Temperature when sensor.Name.Contains("GPU Core") || sensor.Name == "GPU Core":
                    metrics.Gpu.TemperatureCelsius = sensor.Value;
                    break;
                case SensorType.SmallData when sensor.Name.Contains("GPU Memory Used"):
                    metrics.Gpu.MemoryUsedMb = sensor.Value;
                    break;
                case SensorType.SmallData when sensor.Name.Contains("GPU Memory Total"):
                    metrics.Gpu.MemoryTotalMb = sensor.Value;
                    break;
                case SensorType.Power when sensor.Name.Contains("GPU Package") || sensor.Name.Contains("GPU Power"):
                    metrics.Gpu.PowerWatts = sensor.Value;
                    break;
                case SensorType.Fan when sensor.Name.Contains("GPU"):
                    metrics.Gpu.FanSpeedPercent = sensor.Value;
                    break;
                case SensorType.Clock when sensor.Name.Contains("GPU Core"):
                    metrics.Gpu.CoreClockMhz = sensor.Value;
                    break;
                case SensorType.Clock when sensor.Name.Contains("GPU Memory"):
                    metrics.Gpu.MemoryClockMhz = sensor.Value;
                    break;
            }
        }
    }

    private static void UpdateMemoryMetrics(IHardware hardware, SystemMetrics metrics)
    {
        foreach (var sensor in hardware.Sensors)
        {
            switch (sensor.SensorType)
            {
                case SensorType.Load when sensor.Name == "Memory":
                    metrics.Memory!.UsagePercent = sensor.Value ?? 0;
                    break;
                case SensorType.Data when sensor.Name == "Memory Used":
                    metrics.Memory!.UsedGb = sensor.Value ?? 0;
                    break;
                case SensorType.Data when sensor.Name == "Memory Available":
                    metrics.Memory!.AvailableGb = sensor.Value ?? 0;
                    break;
            }
        }

        // Calculate total if we have used + available
        if (metrics.Memory!.UsedGb > 0 && metrics.Memory.AvailableGb > 0)
        {
            metrics.Memory.TotalGb = metrics.Memory.UsedGb + metrics.Memory.AvailableGb;
        }
    }

    private static void UpdateStorageMetrics(IHardware hardware, SystemMetrics metrics)
    {
        var storage = new StorageMetrics { Name = hardware.Name };

        foreach (var sensor in hardware.Sensors)
        {
            switch (sensor.SensorType)
            {
                case SensorType.Temperature:
                    storage.TemperatureCelsius = sensor.Value;
                    break;
                case SensorType.Load when sensor.Name.Contains("Used"):
                    storage.UsagePercent = sensor.Value ?? 0;
                    break;
                case SensorType.Throughput when sensor.Name.Contains("Read"):
                    storage.ReadSpeedMbps = sensor.Value;
                    break;
                case SensorType.Throughput when sensor.Name.Contains("Write"):
                    storage.WriteSpeedMbps = sensor.Value;
                    break;
            }
        }

        if (!string.IsNullOrEmpty(storage.Name))
        {
            metrics.Storage.Add(storage);
        }
    }

    private static void UpdateNetworkMetrics(IHardware hardware, SystemMetrics metrics)
    {
        var network = new NetworkMetrics { Name = hardware.Name };

        foreach (var sensor in hardware.Sensors)
        {
            switch (sensor.SensorType)
            {
                case SensorType.Throughput when sensor.Name.Contains("Download"):
                    network.DownloadSpeedMbps = sensor.Value ?? 0;
                    break;
                case SensorType.Throughput when sensor.Name.Contains("Upload"):
                    network.UploadSpeedMbps = sensor.Value ?? 0;
                    break;
                case SensorType.Data when sensor.Name.Contains("Downloaded"):
                    network.TotalBytesReceived = (long)((sensor.Value ?? 0) * 1024 * 1024 * 1024);
                    break;
                case SensorType.Data when sensor.Name.Contains("Uploaded"):
                    network.TotalBytesSent = (long)((sensor.Value ?? 0) * 1024 * 1024 * 1024);
                    break;
            }
        }

        if (!string.IsNullOrEmpty(network.Name))
        {
            metrics.Network.Add(network);
        }
    }

    public void Dispose()
    {
        _computer?.Close();
        _computer = null;
    }
}
