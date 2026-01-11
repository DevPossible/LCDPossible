using LCDPossible.Core.Monitoring;

namespace LCDPossible.Plugins.Core.Monitoring;

/// <summary>
/// Fallback monitor for unsupported platforms.
/// Uses basic .NET APIs to provide minimal system info.
/// </summary>
internal sealed class FallbackMonitor : IPlatformMonitor
{
    public string PlatformName => "Generic";
    public bool IsAvailable { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsAvailable = true;
        return Task.CompletedTask;
    }

    public Task<SystemMetrics?> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return Task.FromResult<SystemMetrics?>(null);
        }

        // Use basic .NET APIs
        var gcInfo = GC.GetGCMemoryInfo();
        var totalMemory = gcInfo.TotalAvailableMemoryBytes / (1024.0 * 1024.0 * 1024.0);

        // This gives process memory, not system memory - better than nothing
        var usedMemory = GC.GetTotalMemory(false) / (1024.0 * 1024.0 * 1024.0);

        var metrics = new SystemMetrics
        {
            Cpu = new CpuMetrics
            {
                Name = Environment.MachineName,
                UsagePercent = 0, // Cannot easily get without platform-specific code
                CoreUsages = Enumerable.Range(0, Environment.ProcessorCount).Select(_ => 0f).ToList()
            },
            Gpu = new GpuMetrics
            {
                Name = "Unknown",
                UsagePercent = 0
            },
            Memory = new MemoryMetrics
            {
                TotalGb = (float)totalMemory,
                UsedGb = (float)usedMemory,
                AvailableGb = (float)(totalMemory - usedMemory),
                UsagePercent = totalMemory > 0 ? (float)(usedMemory / totalMemory * 100) : 0
            },
            Timestamp = DateTime.UtcNow
        };

        return Task.FromResult<SystemMetrics?>(metrics);
    }

    public void Dispose()
    {
        // No resources to dispose
    }
}
