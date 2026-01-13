using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// Minimalist GPU usage panel with giant percentage display.
/// Optimized for at-a-glance monitoring from 3-6 feet away.
/// </summary>
public sealed class GpuUsageTextPanel : WidgetPanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "gpu-usage-text";
    public override string DisplayName => "GPU Usage Text";

    public GpuUsageTextPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    protected override async Task<object> GetPanelDataAsync(CancellationToken cancellationToken)
    {
        var metrics = await _provider.GetMetricsAsync(cancellationToken);
        var gpu = metrics?.Gpu;

        return new
        {
            hasData = gpu != null,
            usage = gpu?.UsagePercent ?? 0,
            temperature = gpu?.TemperatureCelsius
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

        // GPU Usage - hero sized for at-a-glance reading
        yield return new WidgetDefinition("lcd-stat-card", 4, 4, new
        {
            title = "GPU USAGE",
            value = $"{data.usage:F0}%",
            status = data.usage >= 80 ? "warning" : "",
            size = "hero"
        });

        // Load status - large text indicator
        yield return new WidgetDefinition("lcd-stat-card", 4, 4, new
        {
            title = "LOAD",
            value = data.usage >= 80 ? "HIGH" : data.usage >= 50 ? "MED" : "LOW",
            status = data.usage >= 80 ? "warning" : data.usage >= 50 ? "info" : "success",
            size = "hero"
        });

        // Temperature
        yield return new WidgetDefinition("lcd-stat-card", 4, 4, new
        {
            title = "TEMP",
            value = data.temperature != null ? $"{data.temperature:F0}°C" : "--",
            status = data.temperature != null && data.temperature >= 70 ? "warning" : "",
            size = "hero"
        });
    }
}
