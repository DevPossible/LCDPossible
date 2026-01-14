using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// RAM usage panel with graphical visualization.
/// Shows memory usage with progress bar and breakdown details.
/// </summary>
public sealed class RamUsageGraphicPanel : WidgetPanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "ram-usage-graphic";
    public override string DisplayName => "RAM Usage Graphic";

    public RamUsageGraphicPanel(ISystemInfoProvider provider)
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

        // Top row: Title with value (2 rows for proper sizing) + Progress bar
        yield return new WidgetDefinition("lcd-stat-card", 2, 2, new
        {
            title = "RAM",
            value = $"{data.usagePercent:F0}%",
            status = data.usagePercent >= 80 ? "warning" : "",
            size = "large"
        });

        yield return new WidgetDefinition("lcd-usage-bar", 10, 2, new
        {
            value = data.usagePercent,
            max = 100,
            label = "USAGE",
            showPercent = true
        });

        // Bottom rows: Large donut + Info
        yield return new WidgetDefinition("lcd-donut", 6, 2, new
        {
            value = data.usagePercent,
            max = 100,
            label = "MEMORY"
        });

        yield return new WidgetDefinition("lcd-info-list", 6, 2, new
        {
            title = "MEMORY",
            items = new[]
            {
                new { label = "USED", value = $"{data.usedGb:F1} GB" },
                new { label = "FREE", value = $"{data.availableGb:F1} GB" },
                new { label = "TOTAL", value = $"{data.totalGb:F0} GB" }
            }
        });
    }
}
