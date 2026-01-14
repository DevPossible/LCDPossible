using LCDPossible.Plugins.Proxmox.Api;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Proxmox.Panels;

/// <summary>
/// Panel showing Proxmox VM/Container list using WidgetPanel layout.
/// </summary>
public sealed class ProxmoxVmsWidgetPanel : WidgetPanel
{
    private readonly ProxmoxApiClient _client;
    private ProxmoxMetrics? _metrics;
    private string? _errorMessage;
    private bool _hasSslError;

    public override string PanelId => "proxmox-vms";
    public override string DisplayName => "Proxmox VMs";

    // Show up to 4 items in the main display
    private const int MaxDisplayItems = 4;

    public ProxmoxVmsWidgetPanel(ProxmoxApiClient client)
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
                hasSslError = _hasSslError,
                totalItems = 0,
                displayedItems = 0,
                hiddenItems = 0
            };
        }

        // Combine and sort VMs and containers
        var items = _metrics.VirtualMachines
            .Select(v => new VmItem("VM", v.Name, v.Node, v.Status, v.CpuUsagePercent, v.MemoryUsagePercent, v.IsRunning))
            .Concat(_metrics.Containers
                .Select(c => new VmItem("CT", c.Name, c.Node, c.Status, c.CpuUsagePercent, c.MemoryUsagePercent, c.IsRunning)))
            .OrderByDescending(x => x.IsRunning)
            .ThenBy(x => x.Type)
            .ThenBy(x => x.Name)
            .ToList();

        var displayItems = items.Take(MaxDisplayItems).ToList();

        return new
        {
            hasData = true,
            totalItems = items.Count,
            displayedItems = displayItems.Count,
            hiddenItems = Math.Max(0, items.Count - MaxDisplayItems),
            items = displayItems
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
        // For 1 item: full width
        // For 2 items: 6 columns each (side by side)
        // For 3 items: first 2 at 6 cols, third at 12 cols
        // For 4 items: 2x2 grid (6 cols each)

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
            // Add a small indicator for remaining items
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

    private static string TruncateName(string name, int maxLength)
    {
        if (string.IsNullOrEmpty(name) || name.Length <= maxLength)
            return name;
        return name[..(maxLength - 3)] + "...";
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
