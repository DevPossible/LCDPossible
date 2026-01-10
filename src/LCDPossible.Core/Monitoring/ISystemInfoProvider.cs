namespace LCDPossible.Core.Monitoring;

/// <summary>
/// Interface for system information providers.
/// Implementations can gather metrics from local hardware, remote APIs (Proxmox), etc.
/// </summary>
public interface ISystemInfoProvider : IDisposable
{
    /// <summary>
    /// Gets the name of this provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Indicates whether the provider is currently available and connected.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Initializes the provider. Must be called before collecting metrics.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Collects current system metrics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current system metrics, or null if unavailable.</returns>
    Task<SystemMetrics?> GetMetricsAsync(CancellationToken cancellationToken = default);
}
