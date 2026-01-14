using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Proxmox.Panels;

/// <summary>
/// Demo version of Proxmox summary widget panel showing sample data.
/// </summary>
public sealed class ProxmoxSummaryWidgetDemoPanel : WidgetPanel
{
    public override string PanelId => "proxmox-summary";
    public override string DisplayName => "Proxmox Summary (Demo)";

    protected override Task<object> GetPanelDataAsync(CancellationToken cancellationToken)
    {
        // Return demo data
        return Task.FromResult<object>(new
        {
            hasData = true,
            clusterName = "demo-cluster",
            // Nodes
            nodesOnline = 3,
            nodesTotal = 3,
            nodesHealthy = true,
            // VMs
            vmsRunning = 12,
            vmsTotal = 15,
            // Containers
            containersRunning = 8,
            containersTotal = 10,
            // Alerts
            alertCount = 0,
            hasCriticalAlerts = false,
            hasWarningAlerts = false,
            // Resources
            cpuUsage = 45.5f,
            memoryUsage = 67.3f,
            storageUsage = 52.0f,
            // Top alert
            topAlertTitle = (string?)null,
            topAlertDescription = (string?)null,
            topAlertIsCritical = false
        });
    }

    protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
    {
        dynamic data = panelData;

        // Row 1: Cluster name (left) + 4 summary stats (right)
        // Cluster name - 4 columns
        yield return new WidgetDefinition("lcd-stat-card", 4, 2, new
        {
            title = "PROXMOX CLUSTER",
            value = (string)data.clusterName,
            desc = "Demo Mode",
            status = "info",
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
        yield return new WidgetDefinition("lcd-stat-card", 2, 2, new
        {
            title = "ALERTS",
            value = "OK",
            status = "success",
            size = "large"
        });

        // Row 2: Resource usage bars
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

        // Storage - 4 columns
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
