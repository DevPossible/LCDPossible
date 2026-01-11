namespace LCDPossible.Plugins.Proxmox.Api;

/// <summary>
/// Metrics specific to Proxmox VE clusters.
/// </summary>
public sealed class ProxmoxMetrics
{
    /// <summary>
    /// Cluster name.
    /// </summary>
    public string ClusterName { get; set; } = string.Empty;

    /// <summary>
    /// List of nodes in the cluster.
    /// </summary>
    public List<ProxmoxNodeMetrics> Nodes { get; set; } = [];

    /// <summary>
    /// List of virtual machines across all nodes.
    /// </summary>
    public List<ProxmoxVmMetrics> VirtualMachines { get; set; } = [];

    /// <summary>
    /// List of LXC containers across all nodes.
    /// </summary>
    public List<ProxmoxContainerMetrics> Containers { get; set; } = [];

    /// <summary>
    /// Recent cluster tasks and their status.
    /// </summary>
    public List<ProxmoxTask> RecentTasks { get; set; } = [];

    /// <summary>
    /// Active alerts and notifications.
    /// </summary>
    public List<ProxmoxAlert> Alerts { get; set; } = [];

    /// <summary>
    /// Summary counts.
    /// </summary>
    public ProxmoxSummary Summary { get; set; } = new();

    /// <summary>
    /// Timestamp when metrics were collected.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Summary statistics for quick display.
/// </summary>
public sealed class ProxmoxSummary
{
    /// <summary>
    /// Total number of nodes in cluster.
    /// </summary>
    public int TotalNodes { get; set; }

    /// <summary>
    /// Number of online nodes.
    /// </summary>
    public int OnlineNodes { get; set; }

    /// <summary>
    /// Total number of VMs.
    /// </summary>
    public int TotalVms { get; set; }

    /// <summary>
    /// Number of running VMs.
    /// </summary>
    public int RunningVms { get; set; }

    /// <summary>
    /// Total number of containers.
    /// </summary>
    public int TotalContainers { get; set; }

    /// <summary>
    /// Number of running containers.
    /// </summary>
    public int RunningContainers { get; set; }

    /// <summary>
    /// Number of critical alerts.
    /// </summary>
    public int CriticalAlerts { get; set; }

    /// <summary>
    /// Number of warning alerts.
    /// </summary>
    public int WarningAlerts { get; set; }

    /// <summary>
    /// Cluster-wide CPU usage percentage.
    /// </summary>
    public float CpuUsagePercent { get; set; }

    /// <summary>
    /// Cluster-wide memory usage percentage.
    /// </summary>
    public float MemoryUsagePercent { get; set; }

    /// <summary>
    /// Cluster-wide storage usage percentage.
    /// </summary>
    public float StorageUsagePercent { get; set; }
}

/// <summary>
/// Proxmox node (physical server) metrics.
/// </summary>
public sealed class ProxmoxNodeMetrics
{
    /// <summary>
    /// Node name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Node status (online, offline, unknown).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Whether the node is online.
    /// </summary>
    public bool IsOnline => Status.Equals("online", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// CPU usage percentage.
    /// </summary>
    public float CpuUsagePercent { get; set; }

    /// <summary>
    /// Memory usage percentage.
    /// </summary>
    public float MemoryUsagePercent { get; set; }

    /// <summary>
    /// Total memory in GB.
    /// </summary>
    public float MemoryTotalGb { get; set; }

    /// <summary>
    /// Used memory in GB.
    /// </summary>
    public float MemoryUsedGb { get; set; }

    /// <summary>
    /// Uptime in seconds.
    /// </summary>
    public long UptimeSeconds { get; set; }

    /// <summary>
    /// Number of VMs on this node.
    /// </summary>
    public int VmCount { get; set; }

    /// <summary>
    /// Number of containers on this node.
    /// </summary>
    public int ContainerCount { get; set; }
}

/// <summary>
/// Proxmox virtual machine metrics.
/// </summary>
public sealed class ProxmoxVmMetrics
{
    /// <summary>
    /// VM ID.
    /// </summary>
    public int VmId { get; set; }

    /// <summary>
    /// VM name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Node hosting this VM.
    /// </summary>
    public string Node { get; set; } = string.Empty;

    /// <summary>
    /// VM status (running, stopped, paused, etc.).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Whether the VM is running.
    /// </summary>
    public bool IsRunning => Status.Equals("running", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// CPU usage percentage.
    /// </summary>
    public float CpuUsagePercent { get; set; }

    /// <summary>
    /// Memory usage percentage.
    /// </summary>
    public float MemoryUsagePercent { get; set; }

    /// <summary>
    /// Allocated memory in MB.
    /// </summary>
    public long MemoryAllocatedMb { get; set; }

    /// <summary>
    /// Uptime in seconds (if running).
    /// </summary>
    public long UptimeSeconds { get; set; }

    /// <summary>
    /// Number of CPUs allocated.
    /// </summary>
    public int CpuCores { get; set; }
}

/// <summary>
/// Proxmox LXC container metrics.
/// </summary>
public sealed class ProxmoxContainerMetrics
{
    /// <summary>
    /// Container ID.
    /// </summary>
    public int ContainerId { get; set; }

    /// <summary>
    /// Container name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Node hosting this container.
    /// </summary>
    public string Node { get; set; } = string.Empty;

    /// <summary>
    /// Container status (running, stopped, etc.).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Whether the container is running.
    /// </summary>
    public bool IsRunning => Status.Equals("running", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// CPU usage percentage.
    /// </summary>
    public float CpuUsagePercent { get; set; }

    /// <summary>
    /// Memory usage percentage.
    /// </summary>
    public float MemoryUsagePercent { get; set; }

    /// <summary>
    /// Allocated memory in MB.
    /// </summary>
    public long MemoryAllocatedMb { get; set; }

    /// <summary>
    /// Uptime in seconds (if running).
    /// </summary>
    public long UptimeSeconds { get; set; }
}

/// <summary>
/// Proxmox cluster task.
/// </summary>
public sealed class ProxmoxTask
{
    /// <summary>
    /// Task ID.
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Task type (e.g., qmstart, vzdump, etc.).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Task status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Node where task ran.
    /// </summary>
    public string Node { get; set; } = string.Empty;

    /// <summary>
    /// User who started the task.
    /// </summary>
    public string User { get; set; } = string.Empty;

    /// <summary>
    /// Task start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Task end time (if completed).
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Whether the task failed.
    /// </summary>
    public bool IsFailed => Status.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                            Status.Contains("failed", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Proxmox alert or notification.
/// </summary>
public sealed class ProxmoxAlert
{
    /// <summary>
    /// Alert severity (critical, warning, info).
    /// </summary>
    public AlertSeverity Severity { get; set; }

    /// <summary>
    /// Alert title/summary.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Alert description/details.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Source of the alert (node name, VM name, etc.).
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// When the alert was triggered.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Alert severity levels.
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}
