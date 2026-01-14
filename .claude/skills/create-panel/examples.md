# Panel Creation Examples

Real-world examples from the LCDPossible codebase.

## Example 1: CpuWidgetPanel (Static Widgets)

A panel with fixed layout widgets from a single data source.

```csharp
using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

public sealed class CpuWidgetPanel : WidgetPanel
{
    private readonly ISystemProvider _provider;

    public override string PanelId => "cpu-widget";
    public override string DisplayName => "CPU Info (Widget)";

    public CpuWidgetPanel(ISystemProvider provider) => _provider = provider;

    protected override async Task<object> GetPanelDataAsync(CancellationToken ct)
    {
        var cpu = await _provider.GetCpuMetricsAsync(ct);
        return new
        {
            name = cpu.Name ?? "Unknown CPU",
            usage = cpu.Usage,
            temperature = cpu.Temperature,
            frequency = cpu.Frequency,
            power = cpu.Power,
            cores = cpu.CoreCount,
            threads = cpu.ThreadCount
        };
    }

    protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
    {
        dynamic data = panelData;

        // CPU Name - full width stat card
        yield return new WidgetDefinition("lcd-stat-card", 12, 1, new
        {
            title = "CPU",
            value = (string)data.name,
            size = "small"
        });

        // Usage bar
        yield return new WidgetDefinition("lcd-usage-bar", 6, 1, new
        {
            value = (float)data.usage,
            max = 100,
            label = "Usage"
        });

        // Temperature gauge (if available)
        if (data.temperature > 0)
        {
            yield return new WidgetDefinition("lcd-temp-gauge", 6, 2, new
            {
                value = (float)data.temperature,
                max = 100,
                label = "Temp"
            });
        }

        // Info list with details
        var infoItems = new List<object>
        {
            new { label = "Cores", value = $"{data.cores} / {data.threads}" }
        };

        if (data.frequency > 0)
            infoItems.Add(new { label = "Freq", value = $"{data.frequency:F2} GHz" });

        if (data.power > 0)
            infoItems.Add(new { label = "Power", value = $"{data.power:F1} W" });

        yield return new WidgetDefinition("lcd-info-list", 6, 2, new
        {
            title = "Details",
            items = infoItems
        });
    }
}
```

## Example 2: NetworkWidgetPanel (Variable Items)

A panel displaying 0-N network interfaces with auto-layout.

```csharp
using System.Net.NetworkInformation;
using System.Net.Sockets;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

public sealed class NetworkWidgetPanel : WidgetPanel
{
    public override string PanelId => "network-info";
    public override string DisplayName => "Network Info";

    protected override Task<object> GetPanelDataAsync(CancellationToken ct)
    {
        return Task.FromResult<object>(new
        {
            timestamp = DateTime.Now.ToString("HH:mm:ss")
        });
    }

    protected override Task<IReadOnlyList<object>> GetItemsAsync(CancellationToken ct)
    {
        var interfaces = GetActiveNetworkInterfaces();
        return Task.FromResult<IReadOnlyList<object>>(interfaces.Cast<object>().ToList());
    }

    protected override string GetEmptyStateMessage() => "No active network connections";

    protected override WidgetDefinition? DefineItemWidget(object item, int index, int totalItems)
    {
        var iface = (NetworkInterfaceInfo)item;

        // Calculate layout based on item count
        var (colSpan, rowSpan) = WidgetLayouts.GetAutoLayout(index, totalItems);

        // Build info items - show more details when fewer items
        var infoItems = new List<object>
        {
            new { label = "IP", value = iface.IpAddress ?? "N/A" }
        };

        if (totalItems <= 2)
        {
            // More space - show more details
            if (!string.IsNullOrEmpty(iface.Gateway))
                infoItems.Add(new { label = "Gateway", value = iface.Gateway });
            if (iface.SpeedMbps > 0)
                infoItems.Add(new { label = "Speed", value = FormatLinkSpeed(iface.SpeedMbps) });
        }
        else
        {
            // Less space - minimal info
            if (iface.SpeedMbps > 0)
                infoItems.Add(new { label = "Speed", value = FormatLinkSpeed(iface.SpeedMbps) });
        }

        return new WidgetDefinition("lcd-info-list", colSpan, rowSpan, new
        {
            title = iface.Name,
            subtitle = GetInterfaceTypeDisplay(iface.Type),
            items = infoItems,
            status = "success"
        });
    }
}
```

## Example 3: Conditional Widget Content

Adjust widgets based on data availability:

```csharp
protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
{
    dynamic data = panelData;

    // Always show main stat
    yield return new WidgetDefinition("lcd-stat-card", 6, 2, new
    {
        title = "Usage",
        value = $"{data.usage:F1}",
        unit = "%",
        status = GetStatusForUsage(data.usage)
    });

    // Conditionally show temperature if available
    if (data.temperature > 0)
    {
        yield return new WidgetDefinition("lcd-temp-gauge", 6, 2, new
        {
            value = (float)data.temperature,
            max = 100,
            label = "Temp"
        });
    }
    else
    {
        yield return new WidgetDefinition("lcd-stat-card", 6, 2, new
        {
            title = "Temperature",
            value = "N/A",
            subtitle = "No sensor data",
            status = "neutral"
        });
    }
}

private static string GetStatusForUsage(float usage) => usage switch
{
    > 90 => "critical",
    > 70 => "warning",
    _ => "success"
};
```

## Example 4: Adaptive Detail Levels

Show more info when fewer items displayed:

```csharp
protected override WidgetDefinition? DefineItemWidget(object item, int index, int totalItems)
{
    var vm = (VmInfo)item;
    var (colSpan, rowSpan) = WidgetLayouts.GetAutoLayout(index, totalItems);

    // Base items always shown
    var items = new List<object>
    {
        new { label = "Status", value = vm.Status },
        new { label = "CPU", value = $"{vm.CpuUsage:F0}%" }
    };

    // Add more details when we have space
    if (totalItems <= 2)
    {
        items.Add(new { label = "RAM", value = $"{vm.MemoryUsage:F0}%" });
        items.Add(new { label = "Uptime", value = FormatUptime(vm.Uptime) });
    }
    else if (totalItems <= 4)
    {
        items.Add(new { label = "RAM", value = $"{vm.MemoryUsage:F0}%" });
    }

    return new WidgetDefinition("lcd-info-list", colSpan, rowSpan, new
    {
        title = vm.Name,
        subtitle = vm.Type,
        items = items,
        status = GetWidgetStatus(vm.Status)
    });
}
```

## Plugin Registration Pattern

```csharp
// In CorePlugin.cs

// 1. PanelTypes dictionary
public IReadOnlyDictionary<string, PanelTypeInfo> PanelTypes { get; } = new Dictionary<string, PanelTypeInfo>
{
    ["network-info"] = new PanelTypeInfo
    {
        TypeId = "network-info",
        DisplayName = "Network Info",
        Description = "Network interfaces with IP, gateway, speed",
        Category = "Network",
        IsLive = true
    }
};

// 2. CreatePanel method
public IDisplayPanel? CreatePanel(string panelTypeId, PanelCreationContext context)
{
    IDisplayPanel? panel = panelTypeId.ToLowerInvariant() switch
    {
        "network-info" => new NetworkWidgetPanel(),
        _ => null
    };

    // Set color scheme for WidgetPanel-derived panels
    if (panel is BasePanel basePanel && context.ColorScheme != null)
    {
        basePanel.SetColorScheme(context.ColorScheme);
    }

    return panel;
}
```
