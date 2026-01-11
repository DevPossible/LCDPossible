using System.Management;
using System.Runtime.Versioning;
using LCDPossible.Core.Monitoring;
using LibreHardwareMonitor.Hardware;

namespace LCDPossible.Plugins.Core.Monitoring;

/// <summary>
/// Windows hardware monitor using LibreHardwareMonitor.
/// Provides full hardware data including CPU, GPU, RAM, temps, fans, and power.
/// </summary>
[SupportedOSPlatform("windows")]
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

            // Also update subhardware (e.g., CPU cores for temperature sensors)
            foreach (var subHardware in hardware.SubHardware)
            {
                subHardware.Update();
            }

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
        var coreTemps = new List<float>();

        // Process sensors from main hardware
        ProcessCpuSensors(hardware.Sensors, metrics, coreUsages, coreTemps);

        // Also check subhardware (some CPUs report temperatures there)
        foreach (var subHardware in hardware.SubHardware)
        {
            ProcessCpuSensors(subHardware.Sensors, metrics, coreUsages, coreTemps);
        }

        if (coreUsages.Count > 0)
        {
            metrics.Cpu.CoreUsages = coreUsages.OrderBy(x => x).ToList();
        }

        // If no package temperature found, use average of core temperatures
        if (!metrics.Cpu.TemperatureCelsius.HasValue && coreTemps.Count > 0)
        {
            metrics.Cpu.TemperatureCelsius = coreTemps.Average();
        }

        // Fallback: Try WMI if still no temperature
        if (!metrics.Cpu.TemperatureCelsius.HasValue || metrics.Cpu.TemperatureCelsius <= 0)
        {
            metrics.Cpu.TemperatureCelsius = GetCpuTemperatureFromWmi();
        }
    }

    /// <summary>
    /// Fallback method to get CPU temperature via WMI.
    /// </summary>
    private static float? GetCpuTemperatureFromWmi()
    {
        try
        {
            // Try MSAcpi_ThermalZoneTemperature (requires admin)
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI",
                "SELECT * FROM MSAcpi_ThermalZoneTemperature");

            foreach (var obj in searcher.Get())
            {
                var tempKelvin = Convert.ToDouble(obj["CurrentTemperature"]);
                // WMI returns temperature in tenths of Kelvin
                var tempCelsius = (tempKelvin / 10.0) - 273.15;
                if (tempCelsius > 0 && tempCelsius < 150)
                {
                    return (float)tempCelsius;
                }
            }
        }
        catch
        {
            // WMI query failed, try alternative
        }

        try
        {
            // Try Win32_TemperatureProbe (less common)
            using var searcher = new ManagementObjectSearcher(
                @"root\CIMV2",
                "SELECT * FROM Win32_TemperatureProbe");

            foreach (var obj in searcher.Get())
            {
                var temp = obj["CurrentReading"];
                if (temp != null)
                {
                    var tempCelsius = Convert.ToDouble(temp);
                    if (tempCelsius > 0 && tempCelsius < 150)
                    {
                        return (float)tempCelsius;
                    }
                }
            }
        }
        catch
        {
            // WMI query failed
        }

        return null;
    }

    private static void ProcessCpuSensors(ISensor[] sensors, SystemMetrics metrics, List<float> coreUsages, List<float> coreTemps)
    {
        foreach (var sensor in sensors)
        {
            switch (sensor.SensorType)
            {
                case SensorType.Load when sensor.Name.Contains("Total"):
                    metrics.Cpu!.UsagePercent = sensor.Value ?? 0;
                    break;
                case SensorType.Load when sensor.Name.StartsWith("CPU Core"):
                    coreUsages.Add(sensor.Value ?? 0);
                    break;
                case SensorType.Temperature:
                    // Try various known names for package/overall CPU temperature
                    var name = sensor.Name;
                    var value = sensor.Value;

                    if (!value.HasValue || value.Value <= 0)
                        break;

                    // Prioritized sensor names (Package is highest priority)
                    if (name.Contains("Package"))
                    {
                        metrics.Cpu!.TemperatureCelsius = value;
                    }
                    else if (name.Contains("Tctl") || name.Contains("Tdie"))
                    {
                        // AMD specific - use if no Package temp
                        metrics.Cpu!.TemperatureCelsius ??= value;
                    }
                    else if (name.Contains("Average") || name.Contains("CCD"))
                    {
                        metrics.Cpu!.TemperatureCelsius ??= value;
                    }
                    else if (name.Contains("Core") || name.Contains("CPU"))
                    {
                        // Collect core temperatures as fallback
                        coreTemps.Add(value.Value);
                    }
                    else
                    {
                        // Any other temperature sensor - still collect it
                        coreTemps.Add(value.Value);
                    }
                    break;
                case SensorType.Clock when sensor.Name.Contains("Core"):
                    // Take the first core clock as representative
                    metrics.Cpu!.FrequencyMhz ??= sensor.Value;
                    break;
                case SensorType.Power when sensor.Name.Contains("Package"):
                    metrics.Cpu!.PowerWatts = sensor.Value;
                    break;
            }
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
