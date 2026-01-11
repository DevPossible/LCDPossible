using System.Text.Json;
using System.Text.Json.Serialization;
using LCDPossible.Plugins.Proxmox.Configuration;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Plugins.Proxmox.Api;

/// <summary>
/// Proxmox VE API client for cluster monitoring.
/// </summary>
public sealed class ProxmoxApiClient : IDisposable
{
    private readonly ProxmoxPluginOptions _options;
    private readonly ILogger? _logger;
    private HttpClient? _httpClient;
    private bool _disposed;
    private bool _initialized;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public bool IsAvailable => _initialized && !_disposed && _httpClient != null;

    public ProxmoxApiClient(ProxmoxPluginOptions options, ILogger? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return Task.CompletedTask;
        }

        if (!_options.Enabled)
        {
            _logger?.LogInformation("Proxmox integration is disabled");
            return Task.CompletedTask;
        }

        if (string.IsNullOrEmpty(_options.ApiUrl))
        {
            _logger?.LogWarning("Proxmox API URL not configured");
            return Task.CompletedTask;
        }

        try
        {
            var handler = new HttpClientHandler();

            if (_options.IgnoreSslErrors)
            {
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                _logger?.LogWarning("SSL certificate validation is disabled for Proxmox API");
            }

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(_options.ApiUrl.TrimEnd('/') + "/api2/json/"),
                Timeout = TimeSpan.FromSeconds(30)
            };

            // Set API token authentication
            if (!string.IsNullOrEmpty(_options.TokenId) && !string.IsNullOrEmpty(_options.TokenSecret))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "PVEAPIToken",
                        $"{_options.TokenId}={_options.TokenSecret}");
            }

            _initialized = true;
            _logger?.LogInformation("Proxmox API client initialized for {ApiUrl}", _options.ApiUrl);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Proxmox API client");
            throw;
        }

        return Task.CompletedTask;
    }

    public async Task<ProxmoxMetrics?> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return null;
        }

        var metrics = new ProxmoxMetrics
        {
            Timestamp = DateTime.UtcNow
        };

        try
        {
            // Get cluster status
            var clusterStatus = await GetAsync<ProxmoxApiResponse<List<ClusterStatusItem>>>(
                "cluster/status", cancellationToken);

            if (clusterStatus?.Data != null)
            {
                var cluster = clusterStatus.Data.FirstOrDefault(x => x.Type == "cluster");
                metrics.ClusterName = cluster?.Name ?? "Unknown";
            }

            // Get nodes
            var nodesResponse = await GetAsync<ProxmoxApiResponse<List<NodeItem>>>(
                "nodes", cancellationToken);

            if (nodesResponse?.Data != null)
            {
                foreach (var node in nodesResponse.Data)
                {
                    var nodeMetrics = new ProxmoxNodeMetrics
                    {
                        Name = node.Node,
                        Status = node.Status,
                        CpuUsagePercent = (float)(node.Cpu * 100),
                        MemoryUsedGb = node.Mem / (1024f * 1024f * 1024f),
                        MemoryTotalGb = node.MaxMem / (1024f * 1024f * 1024f),
                        UptimeSeconds = node.Uptime
                    };
                    nodeMetrics.MemoryUsagePercent = nodeMetrics.MemoryTotalGb > 0
                        ? (nodeMetrics.MemoryUsedGb / nodeMetrics.MemoryTotalGb) * 100
                        : 0;

                    metrics.Nodes.Add(nodeMetrics);

                    // Get VMs for this node
                    if (_options.ShowVms)
                    {
                        var vms = await GetVmsForNodeAsync(node.Node, cancellationToken);
                        metrics.VirtualMachines.AddRange(vms);
                        nodeMetrics.VmCount = vms.Count;
                    }

                    // Get containers for this node
                    if (_options.ShowContainers)
                    {
                        var containers = await GetContainersForNodeAsync(node.Node, cancellationToken);
                        metrics.Containers.AddRange(containers);
                        nodeMetrics.ContainerCount = containers.Count;
                    }
                }
            }

            // Get cluster tasks (recent)
            var tasksResponse = await GetAsync<ProxmoxApiResponse<List<TaskItem>>>(
                "cluster/tasks?limit=20", cancellationToken);

            if (tasksResponse?.Data != null)
            {
                foreach (var task in tasksResponse.Data.Take(_options.MaxDisplayItems))
                {
                    metrics.RecentTasks.Add(new ProxmoxTask
                    {
                        TaskId = task.Upid,
                        Type = task.Type,
                        Status = task.Status ?? "running",
                        Node = task.Node,
                        User = task.User,
                        StartTime = DateTimeOffset.FromUnixTimeSeconds(task.StartTime).DateTime,
                        EndTime = task.EndTime > 0
                            ? DateTimeOffset.FromUnixTimeSeconds(task.EndTime).DateTime
                            : null
                    });
                }
            }

            // Check for alerts (high resource usage, failed tasks, etc.)
            GenerateAlerts(metrics);

            // Update summary
            UpdateSummary(metrics);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error fetching Proxmox metrics");
            return null;
        }

        return metrics;
    }

    private async Task<List<ProxmoxVmMetrics>> GetVmsForNodeAsync(string node, CancellationToken cancellationToken)
    {
        var vms = new List<ProxmoxVmMetrics>();

        try
        {
            var response = await GetAsync<ProxmoxApiResponse<List<VmItem>>>(
                $"nodes/{node}/qemu", cancellationToken);

            if (response?.Data != null)
            {
                foreach (var vm in response.Data.Take(_options.MaxDisplayItems))
                {
                    vms.Add(new ProxmoxVmMetrics
                    {
                        VmId = vm.Vmid,
                        Name = vm.Name ?? $"VM {vm.Vmid}",
                        Node = node,
                        Status = vm.Status,
                        CpuUsagePercent = (float)(vm.Cpu * 100),
                        MemoryAllocatedMb = vm.MaxMem / (1024 * 1024),
                        MemoryUsagePercent = vm.MaxMem > 0 ? (float)vm.Mem / vm.MaxMem * 100 : 0,
                        UptimeSeconds = vm.Uptime,
                        CpuCores = vm.Cpus
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get VMs for node {Node}", node);
        }

        return vms;
    }

    private async Task<List<ProxmoxContainerMetrics>> GetContainersForNodeAsync(string node, CancellationToken cancellationToken)
    {
        var containers = new List<ProxmoxContainerMetrics>();

        try
        {
            var response = await GetAsync<ProxmoxApiResponse<List<ContainerItem>>>(
                $"nodes/{node}/lxc", cancellationToken);

            if (response?.Data != null)
            {
                foreach (var ct in response.Data.Take(_options.MaxDisplayItems))
                {
                    containers.Add(new ProxmoxContainerMetrics
                    {
                        ContainerId = ct.Vmid,
                        Name = ct.Name ?? $"CT {ct.Vmid}",
                        Node = node,
                        Status = ct.Status,
                        CpuUsagePercent = (float)(ct.Cpu * 100),
                        MemoryAllocatedMb = ct.MaxMem / (1024 * 1024),
                        MemoryUsagePercent = ct.MaxMem > 0 ? (float)ct.Mem / ct.MaxMem * 100 : 0,
                        UptimeSeconds = ct.Uptime
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get containers for node {Node}", node);
        }

        return containers;
    }

    private void GenerateAlerts(ProxmoxMetrics metrics)
    {
        if (!_options.ShowAlerts)
        {
            return;
        }

        // Check for offline nodes
        foreach (var node in metrics.Nodes.Where(n => !n.IsOnline))
        {
            metrics.Alerts.Add(new ProxmoxAlert
            {
                Severity = AlertSeverity.Critical,
                Title = "Node Offline",
                Description = $"Node '{node.Name}' is offline",
                Source = node.Name,
                Timestamp = DateTime.UtcNow
            });
        }

        // Check for high CPU usage on nodes
        foreach (var node in metrics.Nodes.Where(n => n.IsOnline && n.CpuUsagePercent > 90))
        {
            metrics.Alerts.Add(new ProxmoxAlert
            {
                Severity = AlertSeverity.Warning,
                Title = "High CPU Usage",
                Description = $"Node '{node.Name}' CPU at {node.CpuUsagePercent:F1}%",
                Source = node.Name,
                Timestamp = DateTime.UtcNow
            });
        }

        // Check for high memory usage on nodes
        foreach (var node in metrics.Nodes.Where(n => n.IsOnline && n.MemoryUsagePercent > 90))
        {
            metrics.Alerts.Add(new ProxmoxAlert
            {
                Severity = AlertSeverity.Warning,
                Title = "High Memory Usage",
                Description = $"Node '{node.Name}' memory at {node.MemoryUsagePercent:F1}%",
                Source = node.Name,
                Timestamp = DateTime.UtcNow
            });
        }

        // Check for failed tasks
        foreach (var task in metrics.RecentTasks.Where(t => t.IsFailed))
        {
            metrics.Alerts.Add(new ProxmoxAlert
            {
                Severity = AlertSeverity.Warning,
                Title = "Task Failed",
                Description = $"Task '{task.Type}' failed on node '{task.Node}'",
                Source = task.Node,
                Timestamp = task.EndTime ?? task.StartTime
            });
        }
    }

    private static void UpdateSummary(ProxmoxMetrics metrics)
    {
        metrics.Summary = new ProxmoxSummary
        {
            TotalNodes = metrics.Nodes.Count,
            OnlineNodes = metrics.Nodes.Count(n => n.IsOnline),
            TotalVms = metrics.VirtualMachines.Count,
            RunningVms = metrics.VirtualMachines.Count(v => v.IsRunning),
            TotalContainers = metrics.Containers.Count,
            RunningContainers = metrics.Containers.Count(c => c.IsRunning),
            CriticalAlerts = metrics.Alerts.Count(a => a.Severity == AlertSeverity.Critical),
            WarningAlerts = metrics.Alerts.Count(a => a.Severity == AlertSeverity.Warning),
            CpuUsagePercent = metrics.Nodes.Where(n => n.IsOnline).DefaultIfEmpty().Average(n => n?.CpuUsagePercent ?? 0),
            MemoryUsagePercent = metrics.Nodes.Where(n => n.IsOnline).DefaultIfEmpty().Average(n => n?.MemoryUsagePercent ?? 0)
        };
    }

    private async Task<T?> GetAsync<T>(string endpoint, CancellationToken cancellationToken) where T : class
    {
        if (_httpClient == null)
        {
            return null;
        }

        try
        {
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<T>(content, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "HTTP request failed for endpoint {Endpoint}", endpoint);
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _httpClient?.Dispose();
        _httpClient = null;
        _logger?.LogInformation("Proxmox API client disposed");
    }

    #region API Response DTOs

    private sealed class ProxmoxApiResponse<T>
    {
        public T? Data { get; set; }
    }

    private sealed class ClusterStatusItem
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private sealed class NodeItem
    {
        public string Node { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double Cpu { get; set; }
        public long Mem { get; set; }
        public long MaxMem { get; set; }
        public long Uptime { get; set; }
    }

    private sealed class VmItem
    {
        public int Vmid { get; set; }
        public string? Name { get; set; }
        public string Status { get; set; } = string.Empty;
        public double Cpu { get; set; }
        public long Mem { get; set; }
        public long MaxMem { get; set; }
        public long Uptime { get; set; }
        public int Cpus { get; set; }
    }

    private sealed class ContainerItem
    {
        public int Vmid { get; set; }
        public string? Name { get; set; }
        public string Status { get; set; } = string.Empty;
        public double Cpu { get; set; }
        public long Mem { get; set; }
        public long MaxMem { get; set; }
        public long Uptime { get; set; }
    }

    private sealed class TaskItem
    {
        public string Upid { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Status { get; set; }
        public string Node { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public long StartTime { get; set; }
        public long EndTime { get; set; }
    }

    #endregion
}
