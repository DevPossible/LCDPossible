using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// Minimalist RAM usage panel with giant percentage display.
/// Optimized for at-a-glance monitoring from 3-6 feet away.
/// </summary>
public sealed class RamUsageTextPanel : WidgetPanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "ram-usage-text";
    public override string DisplayName => "RAM Usage Text";

    public RamUsageTextPanel(ISystemInfoProvider provider)
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
                message = "--"
            });
            yield break;
        }

        // Layout: Text-only panel with large stat cards
        // 3 columns × 4 rows - all text, no graphics

        // RAM Usage percentage - hero sized
        yield return new WidgetDefinition("lcd-stat-card", 4, 4, new
        {
            title = "RAM USAGE",
            value = $"{data.usagePercent:F0}%",
            status = data.usagePercent >= 80 ? "warning" : "",
            size = "hero"
        });

        // Used memory
        yield return new WidgetDefinition("lcd-stat-card", 4, 4, new
        {
            title = "USED",
            value = $"{data.usedGb:F1} GB",
            subtitle = $"of {data.totalGb:F0} GB",
            size = "hero"
        });

        // Available memory
        yield return new WidgetDefinition("lcd-stat-card", 4, 4, new
        {
            title = "FREE",
            value = $"{(data.totalGb - data.usedGb):F1} GB",
            status = (data.totalGb - data.usedGb) < 4 ? "warning" : "success",
            size = "hero"
        });
    }
}
