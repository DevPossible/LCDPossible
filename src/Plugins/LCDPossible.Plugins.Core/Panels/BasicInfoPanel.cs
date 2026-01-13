using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// Combined system overview panel showing CPU, RAM, and GPU at a glance.
/// Uses a 3-column layout for quick system status monitoring.
/// </summary>
public sealed class BasicInfoPanel : WidgetPanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "basic-info";
    public override string DisplayName => "Basic Info";

    public BasicInfoPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    protected override async Task<object> GetPanelDataAsync(CancellationToken cancellationToken)
    {
        var metrics = await _provider.GetMetricsAsync(cancellationToken);

        return new
        {
            hasData = metrics != null,
            cpuName = TruncateName(metrics?.Cpu?.Name ?? "Unknown", 25),
            cpuUsage = metrics?.Cpu?.UsagePercent ?? 0,
            cpuTemp = metrics?.Cpu?.TemperatureCelsius,
            ramUsage = metrics?.Memory?.UsagePercent ?? 0,
            ramUsedGb = metrics?.Memory?.UsedGb ?? 0,
            ramTotalGb = metrics?.Memory?.TotalGb ?? 0,
            gpuName = TruncateName(metrics?.Gpu?.Name ?? "Unknown", 25),
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
                message = "System Data Unavailable"
            });
            yield break;
        }

        // Layout: 3 columns × 2 rows (CPU | RAM | GPU)
        // Row 1: Donuts (4 cols each × 2 rows)
        // Row 2: Stats with temp info (4 cols each × 2 rows)

        // Row 1: Large donuts showing usage at a glance
        yield return new WidgetDefinition("lcd-donut", 4, 2, new
        {
            value = data.cpuUsage,
            max = 100,
            label = "CPU"
        });

        yield return new WidgetDefinition("lcd-donut", 4, 2, new
        {
            value = data.ramUsage,
            max = 100,
            label = "RAM"
        });

        yield return new WidgetDefinition("lcd-donut", 4, 2, new
        {
            value = data.gpuUsage,
            max = 100,
            label = "GPU"
        });

        // Row 2: Supporting info - temps and RAM details
        yield return new WidgetDefinition("lcd-stat-card", 4, 2, new
        {
            title = "CPU TEMP",
            value = data.cpuTemp != null ? $"{data.cpuTemp:F0}°C" : "--",
            status = data.cpuTemp != null && data.cpuTemp >= 70 ? "warning" : "",
            size = "large"
        });

        yield return new WidgetDefinition("lcd-stat-card", 4, 2, new
        {
            title = "RAM USED",
            value = $"{data.ramUsedGb:F1} GB",
            subtitle = $"of {data.ramTotalGb:F0} GB",
            size = "large"
        });

        yield return new WidgetDefinition("lcd-stat-card", 4, 2, new
        {
            title = "GPU TEMP",
            value = data.gpuTemp != null ? $"{data.gpuTemp:F0}°C" : "--",
            status = data.gpuTemp != null && data.gpuTemp >= 70 ? "warning" : "",
            size = "large"
        });
    }

    private static string TruncateName(string name, int maxLength)
    {
        if (string.IsNullOrEmpty(name) || name.Length <= maxLength)
            return name;
        return name[..(maxLength - 3)] + "...";
    }

    private static string GetTempStatus(float temp) => temp switch
    {
        >= 85 => "error",
        >= 70 => "warning",
        _ => ""
    };
}
