using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// CPU status panel using the WidgetPanel system.
/// Displays CPU metrics using HTML/CSS web components.
/// </summary>
public sealed class CpuStatusPanel : WidgetPanel
{
    private readonly ISystemInfoProvider _provider;
    private CpuMetrics? _lastMetrics;

    public override string PanelId => "cpu-status";
    public override string DisplayName => "CPU Status";

    public CpuStatusPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    protected override async Task<object> GetPanelDataAsync(CancellationToken cancellationToken)
    {
        var metrics = await _provider.GetMetricsAsync(cancellationToken);
        _lastMetrics = metrics?.Cpu;

        return new
        {
            name = _lastMetrics?.Name ?? "Unknown CPU",
            usage = _lastMetrics?.UsagePercent ?? 0,
            temperature = _lastMetrics?.TemperatureCelsius,
            frequency = _lastMetrics?.FrequencyMhz,
            power = _lastMetrics?.PowerWatts,
            cores = _lastMetrics?.CoreUsages.Count ?? 0,
            coreUsages = _lastMetrics?.CoreUsages ?? [],
            hasData = _lastMetrics != null
        };
    }

    protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
    {
        dynamic data = panelData;

        if (!data.hasData)
        {
            yield return WidgetDefinition.FullWidth("empty-state", 4, new
            {
                message = "CPU Data Unavailable"
            });
            yield break;
        }

        // Top row: CPU name and usage bar (2 rows each for proper sizing)
        yield return new WidgetDefinition("lcd-stat-card", 4, 2, new
        {
            title = "CPU",
            value = data.name,
            size = "medium"
        });

        yield return new WidgetDefinition("lcd-usage-bar", 8, 2, new
        {
            value = data.usage,
            max = 100,
            label = "USAGE",
            showPercent = true
        });

        // Bottom row: Temperature (if available) + Info + Sparkline
        if (data.temperature != null)
        {
            yield return new WidgetDefinition("lcd-temp-gauge", 3, 2, new
            {
                value = data.temperature,
                max = 100,
                label = "TEMP"
            });
        }

        // Info list with frequency, power, cores
        var infoItems = new List<object>();

        if (data.frequency != null)
        {
            infoItems.Add(new { label = "FREQ", value = $"{data.frequency:F0} MHz" });
        }

        if (data.power != null)
        {
            infoItems.Add(new { label = "POWER", value = $"{data.power:F1} W" });
        }

        infoItems.Add(new { label = "CORES", value = data.cores.ToString() });

        var infoColSpan = data.temperature != null ? 4 : 6;
        yield return new WidgetDefinition("lcd-info-list", infoColSpan, 2, new
        {
            title = "STATS",
            items = infoItems
        });

        // Core usage sparkline
        if (data.coreUsages.Count > 0)
        {
            var sparkColSpan = data.temperature != null ? 5 : 6;
            yield return new WidgetDefinition("echarts-sparkline", sparkColSpan, 2, new
            {
                values = data.coreUsages,
                label = "CORE USAGE",
                style = "area"
            });
        }
    }
}
