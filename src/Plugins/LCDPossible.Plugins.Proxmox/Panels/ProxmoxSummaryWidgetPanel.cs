using LCDPossible.Plugins.Proxmox.Api;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Proxmox.Panels;

/// <summary>
/// Panel showing Proxmox cluster summary using WidgetPanel layout.
/// </summary>
public sealed class ProxmoxSummaryWidgetPanel : WidgetPanel
{
    private readonly ProxmoxApiClient _client;
    private ProxmoxMetrics? _metrics;
    private string? _errorMessage;
    private bool _hasSslError;

    public override string PanelId => "proxmox-summary";
    public override string DisplayName => "Proxmox Summary";

    public ProxmoxSummaryWidgetPanel(ProxmoxApiClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await base.InitializeAsync(cancellationToken);
        await _client.InitializeAsync(cancellationToken);
    }

    protected override async Task<object> GetPanelDataAsync(CancellationToken cancellationToken)
    {
        _metrics = await _client.GetMetricsAsync(cancellationToken);
        _errorMessage = _client.LastError;
        _hasSslError = _client.HasSslError;

        if (_metrics == null)
        {
            return new
            {
                hasData = false,
                errorMessage = _errorMessage,
                hasSslError = _hasSslError
            };
        }

        var summary = _metrics.Summary;
        var alertCount = summary.CriticalAlerts + summary.WarningAlerts;
        var topAlert = _metrics.Alerts
            .OrderByDescending(a => a.Severity)
            .FirstOrDefault();

        return new
        {
            hasData = true,
            clusterName = _metrics.ClusterName,
            // Nodes
            nodesOnline = summary.OnlineNodes,
            nodesTotal = summary.TotalNodes,
            nodesHealthy = summary.OnlineNodes == summary.TotalNodes,
            // VMs
            vmsRunning = summary.RunningVms,
            vmsTotal = summary.TotalVms,
            // Containers
            containersRunning = summary.RunningContainers,
            containersTotal = summary.TotalContainers,
            // Alerts
            alertCount,
            hasCriticalAlerts = summary.CriticalAlerts > 0,
            hasWarningAlerts = summary.WarningAlerts > 0,
            // Resources
            cpuUsage = summary.CpuUsagePercent,
            memoryUsage = summary.MemoryUsagePercent,
            storageUsage = summary.StorageUsagePercent,
            // Top alert
            topAlertTitle = topAlert?.Title,
            topAlertDescription = topAlert?.Description,
            topAlertIsCritical = topAlert?.Severity == AlertSeverity.Critical
        };
    }

    protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
    {
        dynamic data = panelData;

        if (!data.hasData)
        {
            // Error state
            yield return WidgetDefinition.FullWidth("lcd-stat-card", 4, new
            {
                title = "PROXMOX CONNECTION ERROR",
                value = data.hasSslError
                    ? "SSL Certificate Error"
                    : "Unable to connect",
                desc = data.hasSslError
                    ? "Run: lcdpossible config set-proxmox --ignore-ssl-errors"
                    : (string?)data.errorMessage ?? "Check Proxmox configuration",
                status = "error"
            });
            yield break;
        }

        // Row 1: Cluster name (left) + 4 summary stats (right)
        // Cluster name - 4 columns
        yield return new WidgetDefinition("lcd-stat-card", 4, 2, new
        {
            title = "PROXMOX CLUSTER",
            value = TruncateName((string)data.clusterName, 16),
            size = "large"
        });

        // Nodes - 2 columns
        yield return new WidgetDefinition("lcd-stat-card", 2, 2, new
        {
            title = "NODES",
            value = $"{data.nodesOnline}/{data.nodesTotal}",
            status = data.nodesHealthy ? "success" : "warning",
            size = "large"
        });

        // VMs - 2 columns
        yield return new WidgetDefinition("lcd-stat-card", 2, 2, new
        {
            title = "VMs",
            value = $"{data.vmsRunning}/{data.vmsTotal}",
            size = "large"
        });

        // Containers - 2 columns
        yield return new WidgetDefinition("lcd-stat-card", 2, 2, new
        {
            title = "LXC",
            value = $"{data.containersRunning}/{data.containersTotal}",
            status = "success",
            size = "large"
        });

        // Alerts - 2 columns
        string alertStatus = data.hasCriticalAlerts ? "error" :
                            data.hasWarningAlerts ? "warning" : "success";
        yield return new WidgetDefinition("lcd-stat-card", 2, 2, new
        {
            title = "ALERTS",
            value = data.alertCount > 0 ? data.alertCount.ToString() : "OK",
            status = alertStatus,
            size = "large"
        });

        // Row 2: Resource usage bars + Alert message
        // CPU usage - 4 columns
        yield return new WidgetDefinition("lcd-usage-bar", 4, 2, new
        {
            label = "CPU",
            value = data.cpuUsage,
            max = 100,
            showPercent = true,
            orientation = "horizontal"
        });

        // Memory usage - 4 columns
        yield return new WidgetDefinition("lcd-usage-bar", 4, 2, new
        {
            label = "MEMORY",
            value = data.memoryUsage,
            max = 100,
            showPercent = true,
            orientation = "horizontal"
        });

        // Alert message or storage - 4 columns
        if (data.topAlertTitle != null)
        {
            yield return new WidgetDefinition("lcd-stat-card", 4, 2, new
            {
                title = data.topAlertIsCritical ? "CRITICAL ALERT" : "WARNING",
                value = TruncateName((string)data.topAlertTitle, 20),
                desc = TruncateName((string?)data.topAlertDescription ?? "", 30),
                status = data.topAlertIsCritical ? "error" : "warning",
                size = "small"
            });
        }
        else
        {
            yield return new WidgetDefinition("lcd-usage-bar", 4, 2, new
            {
                label = "STORAGE",
                value = data.storageUsage,
                max = 100,
                showPercent = true,
                orientation = "horizontal"
            });
        }
    }

    private static string TruncateName(string name, int maxLength)
    {
        if (string.IsNullOrEmpty(name) || name.Length <= maxLength)
            return name;
        return name[..(maxLength - 3)] + "...";
    }
}
