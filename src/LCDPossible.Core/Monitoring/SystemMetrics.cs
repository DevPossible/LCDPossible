namespace LCDPossible.Core.Monitoring;

/// <summary>
/// System metrics collected from hardware or external sources.
/// </summary>
public sealed class SystemMetrics
{
    /// <summary>
    /// CPU metrics.
    /// </summary>
    public CpuMetrics? Cpu { get; set; }

    /// <summary>
    /// GPU metrics (primary GPU).
    /// </summary>
    public GpuMetrics? Gpu { get; set; }

    /// <summary>
    /// Memory metrics.
    /// </summary>
    public MemoryMetrics? Memory { get; set; }

    /// <summary>
    /// Storage metrics.
    /// </summary>
    public List<StorageMetrics> Storage { get; set; } = [];

    /// <summary>
    /// Network metrics.
    /// </summary>
    public List<NetworkMetrics> Network { get; set; } = [];

    /// <summary>
    /// Timestamp when metrics were collected.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// CPU-specific metrics.
/// </summary>
public sealed class CpuMetrics
{
    /// <summary>
    /// CPU name/model.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Overall CPU usage percentage (0-100).
    /// </summary>
    public float UsagePercent { get; set; }

    /// <summary>
    /// CPU temperature in Celsius.
    /// </summary>
    public float? TemperatureCelsius { get; set; }

    /// <summary>
    /// Per-core usage percentages.
    /// </summary>
    public List<float> CoreUsages { get; set; } = [];

    /// <summary>
    /// CPU power consumption in watts.
    /// </summary>
    public float? PowerWatts { get; set; }

    /// <summary>
    /// Current CPU frequency in MHz.
    /// </summary>
    public float? FrequencyMhz { get; set; }
}

/// <summary>
/// GPU-specific metrics.
/// </summary>
public sealed class GpuMetrics
{
    /// <summary>
    /// GPU name/model.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// GPU core usage percentage (0-100).
    /// </summary>
    public float UsagePercent { get; set; }

    /// <summary>
    /// GPU temperature in Celsius.
    /// </summary>
    public float? TemperatureCelsius { get; set; }

    /// <summary>
    /// GPU memory usage percentage (0-100).
    /// </summary>
    public float? MemoryUsagePercent { get; set; }

    /// <summary>
    /// GPU memory used in MB.
    /// </summary>
    public float? MemoryUsedMb { get; set; }

    /// <summary>
    /// GPU total memory in MB.
    /// </summary>
    public float? MemoryTotalMb { get; set; }

    /// <summary>
    /// GPU power consumption in watts.
    /// </summary>
    public float? PowerWatts { get; set; }

    /// <summary>
    /// GPU fan speed percentage (0-100).
    /// </summary>
    public float? FanSpeedPercent { get; set; }

    /// <summary>
    /// GPU core clock in MHz.
    /// </summary>
    public float? CoreClockMhz { get; set; }

    /// <summary>
    /// GPU memory clock in MHz.
    /// </summary>
    public float? MemoryClockMhz { get; set; }
}

/// <summary>
/// Memory-specific metrics.
/// </summary>
public sealed class MemoryMetrics
{
    /// <summary>
    /// Total physical memory in GB.
    /// </summary>
    public float TotalGb { get; set; }

    /// <summary>
    /// Used physical memory in GB.
    /// </summary>
    public float UsedGb { get; set; }

    /// <summary>
    /// Available physical memory in GB.
    /// </summary>
    public float AvailableGb { get; set; }

    /// <summary>
    /// Memory usage percentage (0-100).
    /// </summary>
    public float UsagePercent { get; set; }
}

/// <summary>
/// Storage/disk metrics.
/// </summary>
public sealed class StorageMetrics
{
    /// <summary>
    /// Drive name or mount point.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Total capacity in GB.
    /// </summary>
    public float TotalGb { get; set; }

    /// <summary>
    /// Used space in GB.
    /// </summary>
    public float UsedGb { get; set; }

    /// <summary>
    /// Free space in GB.
    /// </summary>
    public float FreeGb { get; set; }

    /// <summary>
    /// Usage percentage (0-100).
    /// </summary>
    public float UsagePercent { get; set; }

    /// <summary>
    /// Drive temperature in Celsius (if available).
    /// </summary>
    public float? TemperatureCelsius { get; set; }

    /// <summary>
    /// Read speed in MB/s.
    /// </summary>
    public float? ReadSpeedMbps { get; set; }

    /// <summary>
    /// Write speed in MB/s.
    /// </summary>
    public float? WriteSpeedMbps { get; set; }
}

/// <summary>
/// Network interface metrics.
/// </summary>
public sealed class NetworkMetrics
{
    /// <summary>
    /// Network interface name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Download speed in MB/s.
    /// </summary>
    public float DownloadSpeedMbps { get; set; }

    /// <summary>
    /// Upload speed in MB/s.
    /// </summary>
    public float UploadSpeedMbps { get; set; }

    /// <summary>
    /// Total bytes received.
    /// </summary>
    public long TotalBytesReceived { get; set; }

    /// <summary>
    /// Total bytes sent.
    /// </summary>
    public long TotalBytesSent { get; set; }
}
