using LCDPossible.Core.Monitoring;
using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Monitoring;

/// <summary>
/// Local hardware monitoring provider using LibreHardwareMonitor.
/// Windows-specific implementation for CPU, GPU, and system metrics.
/// </summary>
public sealed class LocalHardwareProvider : ISystemInfoProvider
{
    private readonly ILogger<LocalHardwareProvider>? _logger;
    private Computer? _computer;
    private bool _disposed;

    public string Name => "Local Hardware Monitor";
    public bool IsAvailable => _computer != null && !_disposed;

    public LocalHardwareProvider(ILogger<LocalHardwareProvider>? logger = null)
    {
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_computer != null)
        {
            return Task.CompletedTask;
        }

        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsStorageEnabled = true,
                IsNetworkEnabled = true,
                IsMotherboardEnabled = false, // Not needed for basic metrics
                IsControllerEnabled = false
            };

            _computer.Open();
            _logger?.LogInformation("Local hardware monitoring initialized");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize hardware monitoring");
            throw;
        }

        return Task.CompletedTask;
    }

    public Task<SystemMetrics?> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        if (_computer == null || _disposed)
        {
            return Task.FromResult<SystemMetrics?>(null);
        }

        var metrics = new SystemMetrics
        {
            Timestamp = DateTime.UtcNow
        };

        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();

                switch (hardware.HardwareType)
                {
                    case HardwareType.Cpu:
                        metrics.Cpu = ExtractCpuMetrics(hardware);
                        break;

                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuAmd:
                    case HardwareType.GpuIntel:
                        // Take first GPU found
                        if (metrics.Gpu == null)
                        {
                            metrics.Gpu = ExtractGpuMetrics(hardware);
                        }
                        break;

                    case HardwareType.Memory:
                        metrics.Memory = ExtractMemoryMetrics(hardware);
                        break;

                    case HardwareType.Storage:
                        var storage = ExtractStorageMetrics(hardware);
                        if (storage != null)
                        {
                            metrics.Storage.Add(storage);
                        }
                        break;

                    case HardwareType.Network:
                        var network = ExtractNetworkMetrics(hardware);
                        if (network != null)
                        {
                            metrics.Network.Add(network);
                        }
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error collecting hardware metrics");
        }

        return Task.FromResult<SystemMetrics?>(metrics);
    }

    private CpuMetrics ExtractCpuMetrics(IHardware hardware)
    {
        var cpu = new CpuMetrics
        {
            Name = hardware.Name
        };

        foreach (var sensor in hardware.Sensors)
        {
            if (!sensor.Value.HasValue)
            {
                continue;
            }

            var value = sensor.Value.Value;

            switch (sensor.SensorType)
            {
                case SensorType.Load when sensor.Name.Contains("Total"):
                    cpu.UsagePercent = value;
                    break;

                case SensorType.Load when sensor.Name.Contains("Core"):
                    cpu.CoreUsages.Add(value);
                    break;

                case SensorType.Temperature when sensor.Name.Contains("Package") || sensor.Name.Contains("Core"):
                    // Take package temp if available, otherwise first core temp
                    if (cpu.TemperatureCelsius == null || sensor.Name.Contains("Package"))
                    {
                        cpu.TemperatureCelsius = value;
                    }
                    break;

                case SensorType.Power when sensor.Name.Contains("Package"):
                    cpu.PowerWatts = value;
                    break;

                case SensorType.Clock when sensor.Name.Contains("Core"):
                    // Take first core clock as representative
                    if (cpu.FrequencyMhz == null)
                    {
                        cpu.FrequencyMhz = value;
                    }
                    break;
            }
        }

        // Also check subhardware
        foreach (var sub in hardware.SubHardware)
        {
            sub.Update();
            foreach (var sensor in sub.Sensors)
            {
                if (!sensor.Value.HasValue)
                {
                    continue;
                }

                if (sensor.SensorType == SensorType.Temperature &&
                    cpu.TemperatureCelsius == null)
                {
                    cpu.TemperatureCelsius = sensor.Value.Value;
                }
            }
        }

        return cpu;
    }

    private GpuMetrics ExtractGpuMetrics(IHardware hardware)
    {
        var gpu = new GpuMetrics
        {
            Name = hardware.Name
        };

        foreach (var sensor in hardware.Sensors)
        {
            if (!sensor.Value.HasValue)
            {
                continue;
            }

            var value = sensor.Value.Value;

            switch (sensor.SensorType)
            {
                case SensorType.Load when sensor.Name.Contains("Core") || sensor.Name.Equals("GPU Core"):
                    gpu.UsagePercent = value;
                    break;

                case SensorType.Load when sensor.Name.Contains("Memory"):
                    gpu.MemoryUsagePercent = value;
                    break;

                case SensorType.Temperature when sensor.Name.Contains("Core") || sensor.Name.Equals("GPU Core"):
                    gpu.TemperatureCelsius = value;
                    break;

                case SensorType.SmallData when sensor.Name.Contains("Memory Used"):
                    gpu.MemoryUsedMb = value;
                    break;

                case SensorType.SmallData when sensor.Name.Contains("Memory Total"):
                    gpu.MemoryTotalMb = value;
                    break;

                case SensorType.Power when sensor.Name.Contains("GPU"):
                    gpu.PowerWatts = value;
                    break;

                case SensorType.Fan when sensor.Name.Contains("GPU"):
                    gpu.FanSpeedPercent = value;
                    break;

                case SensorType.Clock when sensor.Name.Contains("Core"):
                    gpu.CoreClockMhz = value;
                    break;

                case SensorType.Clock when sensor.Name.Contains("Memory"):
                    gpu.MemoryClockMhz = value;
                    break;
            }
        }

        return gpu;
    }

    private MemoryMetrics ExtractMemoryMetrics(IHardware hardware)
    {
        var memory = new MemoryMetrics();

        foreach (var sensor in hardware.Sensors)
        {
            if (!sensor.Value.HasValue)
            {
                continue;
            }

            var value = sensor.Value.Value;

            switch (sensor.SensorType)
            {
                case SensorType.Load when sensor.Name.Contains("Memory"):
                    memory.UsagePercent = value;
                    break;

                case SensorType.Data when sensor.Name.Contains("Used"):
                    memory.UsedGb = value;
                    break;

                case SensorType.Data when sensor.Name.Contains("Available"):
                    memory.AvailableGb = value;
                    break;
            }
        }

        // Calculate total if we have used and available
        if (memory.UsedGb > 0 && memory.AvailableGb > 0)
        {
            memory.TotalGb = memory.UsedGb + memory.AvailableGb;
        }

        return memory;
    }

    private StorageMetrics? ExtractStorageMetrics(IHardware hardware)
    {
        var storage = new StorageMetrics
        {
            Name = hardware.Name
        };

        foreach (var sensor in hardware.Sensors)
        {
            if (!sensor.Value.HasValue)
            {
                continue;
            }

            var value = sensor.Value.Value;

            switch (sensor.SensorType)
            {
                case SensorType.Temperature:
                    storage.TemperatureCelsius = value;
                    break;

                case SensorType.Load when sensor.Name.Contains("Used"):
                    storage.UsagePercent = value;
                    break;

                case SensorType.Throughput when sensor.Name.Contains("Read"):
                    storage.ReadSpeedMbps = value;
                    break;

                case SensorType.Throughput when sensor.Name.Contains("Write"):
                    storage.WriteSpeedMbps = value;
                    break;
            }
        }

        return storage;
    }

    private NetworkMetrics? ExtractNetworkMetrics(IHardware hardware)
    {
        var network = new NetworkMetrics
        {
            Name = hardware.Name
        };

        foreach (var sensor in hardware.Sensors)
        {
            if (!sensor.Value.HasValue)
            {
                continue;
            }

            var value = sensor.Value.Value;

            switch (sensor.SensorType)
            {
                case SensorType.Throughput when sensor.Name.Contains("Download"):
                    network.DownloadSpeedMbps = value / (1024 * 1024); // Convert to MB/s
                    break;

                case SensorType.Throughput when sensor.Name.Contains("Upload"):
                    network.UploadSpeedMbps = value / (1024 * 1024); // Convert to MB/s
                    break;

                case SensorType.Data when sensor.Name.Contains("Downloaded"):
                    network.TotalBytesReceived = (long)(value * 1024 * 1024 * 1024); // GB to bytes
                    break;

                case SensorType.Data when sensor.Name.Contains("Uploaded"):
                    network.TotalBytesSent = (long)(value * 1024 * 1024 * 1024); // GB to bytes
                    break;
            }
        }

        return network;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _computer?.Close();
        _computer = null;
        _logger?.LogInformation("Local hardware monitoring disposed");
    }
}
