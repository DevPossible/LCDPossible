namespace LCDPossible.Core.Monitoring;

/// <summary>
/// Interface for Proxmox VE API integration.
/// </summary>
public interface IProxmoxProvider : ISystemInfoProvider
{
    /// <summary>
    /// Gets Proxmox-specific metrics including VMs, containers, and cluster status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Proxmox metrics, or null if unavailable.</returns>
    Task<ProxmoxMetrics?> GetProxmoxMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of a specific VM.
    /// </summary>
    /// <param name="node">Node name.</param>
    /// <param name="vmId">VM ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ProxmoxVmMetrics?> GetVmStatusAsync(string node, int vmId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of a specific container.
    /// </summary>
    /// <param name="node">Node name.</param>
    /// <param name="containerId">Container ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ProxmoxContainerMetrics?> GetContainerStatusAsync(string node, int containerId, CancellationToken cancellationToken = default);
}
