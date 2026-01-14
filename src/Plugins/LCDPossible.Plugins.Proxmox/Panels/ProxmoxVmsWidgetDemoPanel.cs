using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Proxmox.Panels;

/// <summary>
/// Demo version of Proxmox VMs widget panel showing sample data.
/// </summary>
public sealed class ProxmoxVmsWidgetDemoPanel : WidgetPanel
{
    public override string PanelId => "proxmox-vms";
    public override string DisplayName => "Proxmox VMs (Demo)";

    // Demo items
    private static readonly List<VmItem> DemoItems =
    [
        new VmItem("VM", "web-server", "node1", "running", 25.5f, 45.2f, true),
        new VmItem("VM", "db-server", "node1", "running", 15.3f, 62.8f, true),
        new VmItem("CT", "nginx-proxy", "node2", "running", 8.2f, 22.1f, true),
        new VmItem("VM", "dev-vm", "node2", "stopped", 0f, 0f, false)
    ];

    protected override Task<object> GetPanelDataAsync(CancellationToken cancellationToken)
    {
        // Return demo data
        return Task.FromResult<object>(new
        {
            hasData = true,
            totalItems = DemoItems.Count,
            displayedItems = Math.Min(4, DemoItems.Count),
            hiddenItems = Math.Max(0, DemoItems.Count - 4),
            items = DemoItems.Take(4).ToList()
        });
    }

    protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
    {
        dynamic data = panelData;

        if (!data.hasData)
        {
            yield return WidgetDefinition.FullWidth("lcd-stat-card", 4, new
            {
                title = "PROXMOX CONNECTION ERROR",
                value = "Unable to connect",
                desc = "Demo mode - configure Proxmox API for live data",
                status = "error"
            });
            yield break;
        }

        int totalItems = data.totalItems;
        int hiddenItems = data.hiddenItems;

        if (totalItems == 0)
        {
            yield return WidgetDefinition.FullWidth("lcd-stat-card", 4, new
            {
                title = "VMs & CONTAINERS",
                value = "No VMs or Containers",
                desc = "Create VMs or containers in Proxmox",
                status = "info"
            });
            yield break;
        }

        // Get the items list
        var items = (IEnumerable<object>)data.items;
        var itemList = items.Cast<VmItem>().ToList();

        // Layout based on item count (1-4 items)
        for (int i = 0; i < itemList.Count; i++)
        {
            var item = itemList[i];
            var (colSpan, rowSpan) = GetItemLayout(i, itemList.Count);

            yield return new WidgetDefinition("lcd-info-list", colSpan, rowSpan, new
            {
                title = item.Name,
                subtitle = $"{item.Type} on {item.Node}",
                status = item.IsRunning ? "success" : "neutral",
                items = new object[]
                {
                    new { label = "Status", value = item.Status, color = item.IsRunning ? "success" : "neutral" },
                    new { label = "CPU", value = item.IsRunning ? $"{item.CpuUsage:F0}%" : "-", color = (string?)null },
                    new { label = "Memory", value = item.IsRunning ? $"{item.MemoryUsage:F0}%" : "-", color = (string?)null }
                }
            });
        }

        // Show overflow indicator if there are more items
        if (hiddenItems > 0)
        {
            yield return new WidgetDefinition("lcd-stat-card", 12, 1, new
            {
                title = "",
                value = $"+{hiddenItems} more VMs/Containers",
                size = "small",
                status = "info"
            });
        }
    }

    private static (int ColSpan, int RowSpan) GetItemLayout(int index, int totalItems)
    {
        return totalItems switch
        {
            1 => (12, 4),          // Full width, full height
            2 => (6, 4),           // Half width, full height
            3 => index < 2
                ? (6, 2)           // First two: half width, half height
                : (12, 2),         // Third: full width, half height
            _ => (6, 2)            // 4+: 2x2 grid
        };
    }

    /// <summary>
    /// Internal record for VM/Container items.
    /// </summary>
    private sealed record VmItem(
        string Type,
        string Name,
        string Node,
        string Status,
        float CpuUsage,
        float MemoryUsage,
        bool IsRunning
    );
}
