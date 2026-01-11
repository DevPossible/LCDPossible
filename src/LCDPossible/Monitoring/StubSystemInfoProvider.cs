using LCDPossible.Core.Monitoring;

namespace LCDPossible.Monitoring;

/// <summary>
/// Stub implementation of ISystemInfoProvider for when no hardware monitoring is needed.
/// The actual hardware monitoring is done by the Core plugin which loads LibreHardwareMonitor.
/// </summary>
public sealed class StubSystemInfoProvider : ISystemInfoProvider
{
    public string Name => "Stub";

    public bool IsAvailable { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsAvailable = true;
        return Task.CompletedTask;
    }

    public Task<SystemMetrics?> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        // Return empty metrics - the Core plugin provides real data
        var metrics = new SystemMetrics
        {
            Cpu = new CpuMetrics
            {
                Name = "Unknown",
                UsagePercent = 0
            },
            Gpu = new GpuMetrics
            {
                Name = "Unknown",
                UsagePercent = 0
            },
            Memory = new MemoryMetrics
            {
                TotalGb = 0,
                UsedGb = 0,
                AvailableGb = 0,
                UsagePercent = 0
            },
            Timestamp = DateTime.UtcNow
        };
        return Task.FromResult<SystemMetrics?>(metrics);
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}
