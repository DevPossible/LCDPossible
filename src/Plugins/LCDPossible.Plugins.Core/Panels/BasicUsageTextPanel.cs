using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// Minimalist system usage panel showing CPU, RAM, GPU percentages.
/// Large text optimized for at-a-glance monitoring.
/// </summary>
public sealed class BasicUsageTextPanel : WidgetPanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "basic-usage-text";
    public override string DisplayName => "Basic Usage Text";

    public BasicUsageTextPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    protected override async Task<object> GetPanelDataAsync(CancellationToken cancellationToken)
    {
        var metrics = await _provider.GetMetricsAsync(cancellationToken);

        return new
        {
            hasData = metrics != null,
            cpuUsage = metrics?.Cpu?.UsagePercent ?? 0,
            cpuTemp = metrics?.Cpu?.TemperatureCelsius,
            ramUsage = metrics?.Memory?.UsagePercent ?? 0,
            ramUsedGb = metrics?.Memory?.UsedGb ?? 0,
            gpuUsage = metrics?.Gpu?.UsagePercent ?? 0,
            gpuTemp = metrics?.Gpu?.TemperatureCelsius
        };
    }

    protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
    {
        dynamic data = panelData;

        if (!data.hasData)
        {
            yield return WidgetDefinition.FullWidth("empty-state", 4, new
            {
                message = "---"
            });
            yield break;
        }

        // Layout: 6 equal columns spanning full height
        // Each section gets 2 cols × 4 rows for maximum impact

        // CPU Usage - hero stat card (4 rows, needs xlarge)
        yield return new WidgetDefinition("lcd-stat-card", 2, 4, new
        {
            title = "CPU",
            value = $"{data.cpuUsage:F0}%",
            status = GetUsageStatus((float)data.cpuUsage),
            size = "xlarge"
        });

        // CPU Temp
        yield return new WidgetDefinition("lcd-stat-card", 2, 4, new
        {
            title = "TEMP",
            value = data.cpuTemp != null ? $"{data.cpuTemp:F0}°" : "--",
            status = data.cpuTemp != null ? GetTempStatus((float)data.cpuTemp) : "",
            size = "xlarge"
        });

        // RAM Usage
        yield return new WidgetDefinition("lcd-stat-card", 2, 4, new
        {
            title = "RAM",
            value = $"{data.ramUsage:F0}%",
            status = GetUsageStatus((float)data.ramUsage),
            size = "xlarge"
        });

        // RAM Amount
        yield return new WidgetDefinition("lcd-stat-card", 2, 4, new
        {
            title = "USED",
            value = $"{data.ramUsedGb:F0}GB",
            size = "xlarge"
        });

        // GPU Usage
        yield return new WidgetDefinition("lcd-stat-card", 2, 4, new
        {
            title = "GPU",
            value = $"{data.gpuUsage:F0}%",
            status = GetUsageStatus((float)data.gpuUsage),
            size = "xlarge"
        });

        // GPU Temp
        yield return new WidgetDefinition("lcd-stat-card", 2, 4, new
        {
            title = "TEMP",
            value = data.gpuTemp != null ? $"{data.gpuTemp:F0}°" : "--",
            status = data.gpuTemp != null ? GetTempStatus((float)data.gpuTemp) : "",
            size = "xlarge"
        });
    }

    private static string GetUsageStatus(float percent) => percent switch
    {
        >= 90 => "error",
        >= 70 => "warning",
        _ => ""
    };

    private static string GetTempStatus(float temp) => temp switch
    {
        >= 85 => "error",
        >= 70 => "warning",
        _ => ""
    };
}
