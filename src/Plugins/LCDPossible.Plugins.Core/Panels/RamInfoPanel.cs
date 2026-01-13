using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// Memory info panel showing detailed RAM information with usage visualization.
/// Uses DaisyUI components for a modern dashboard appearance.
/// </summary>
public sealed class RamInfoPanel : WidgetPanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "ram-info";
    public override string DisplayName => "RAM Info";

    public RamInfoPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    protected override async Task<object> GetPanelDataAsync(CancellationToken cancellationToken)
    {
        var metrics = await _provider.GetMetricsAsync(cancellationToken);
        var mem = metrics?.Memory;

        return new
        {
            hasData = mem != null,
            usagePercent = mem?.UsagePercent ?? 0,
            usedGb = mem?.UsedGb ?? 0,
            availableGb = mem?.AvailableGb ?? 0,
            totalGb = mem?.TotalGb ?? 0
        };
    }

    protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
    {
        dynamic data = panelData;

        if (!data.hasData)
        {
            yield return WidgetDefinition.FullWidth("empty-state", 4, new
            {
                message = "RAM Data Unavailable"
            });
            yield break;
        }

        // Row 1: Title stat card
        yield return new WidgetDefinition("stat", 6, 1, new
        {
            title = "MEMORY",
            value = $"{data.totalGb:F0} GB",
            desc = "Total System Memory"
        });

        // Row 1 right: Usage donut
        yield return new WidgetDefinition("radial-progress", 6, 2, new
        {
            value = data.usagePercent,
            max = 100,
            label = "Usage",
            size = "10rem"
        });

        // Row 2: Usage bar
        yield return new WidgetDefinition("progress-bar", 6, 1, new
        {
            value = data.usagePercent,
            max = 100,
            label = "Memory Usage",
            showPercent = true
        });

        // Row 3-4: Info list with memory details
        yield return new WidgetDefinition("info-list", 6, 2, new
        {
            title = "Memory Details",
            items = new[]
            {
                new { label = "Used", value = $"{data.usedGb:F1} GB" },
                new { label = "Available", value = $"{data.availableGb:F1} GB" },
                new { label = "Total", value = $"{data.totalGb:F1} GB" }
            }
        });
    }
}
