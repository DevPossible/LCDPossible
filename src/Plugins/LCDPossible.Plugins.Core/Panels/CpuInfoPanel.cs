using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// CPU info panel showing detailed CPU information with usage and temperature gauges.
/// Uses horizontal layout optimized for 1280x480 wide displays.
/// </summary>
public sealed class CpuInfoPanel : WidgetPanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "cpu-info";
    public override string DisplayName => "CPU Info";

    public CpuInfoPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    protected override async Task<object> GetPanelDataAsync(CancellationToken cancellationToken)
    {
        var metrics = await _provider.GetMetricsAsync(cancellationToken);
        var cpu = metrics?.Cpu;

        return new
        {
            hasData = cpu != null,
            name = cpu?.Name ?? "Unknown CPU",
            usage = cpu?.UsagePercent ?? 0,
            temperature = cpu?.TemperatureCelsius,
            frequency = cpu?.FrequencyMhz,
            power = cpu?.PowerWatts,
            cores = cpu?.CoreUsages.Count ?? 0,
            coreUsages = cpu?.CoreUsages ?? []
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

        // Row 1: CPU name (left) + Usage donut (center-right) + Temp gauge (right)
        // CPU Name - spanning most of left side
        yield return new WidgetDefinition("lcd-stat-card", 5, 2, new
        {
            title = "CPU",
            value = TruncateName((string)data.name, 32),
            size = "large"
        });

        // Usage donut - prominent center display
        yield return new WidgetDefinition("lcd-donut", 3, 2, new
        {
            value = data.usage,
            max = 100,
            label = "Usage"
        });

        // Temperature gauge (if available)
        if (data.temperature != null)
        {
            yield return new WidgetDefinition("lcd-temp-gauge", 4, 2, new
            {
                value = data.temperature,
                max = 100,
                label = "Temp"
            });
        }
        else
        {
            // Fill space with additional info
            yield return new WidgetDefinition("lcd-info-list", 4, 2, new
            {
                items = BuildInfoItems(data)
            });
        }

        // Row 2: Info items + Core sparkline
        var hasTemp = data.temperature != null;

        // Info list with frequency, power, cores
        yield return new WidgetDefinition("lcd-info-list", hasTemp ? 5 : 4, 2, new
        {
            items = BuildInfoItems(data)
        });

        // Core usage sparkline (if cores available)
        if (data.coreUsages.Count > 0)
        {
            yield return new WidgetDefinition("echarts-sparkline", hasTemp ? 7 : 8, 2, new
            {
                values = data.coreUsages,
                label = "Core Usage",
                style = "area"
            });
        }
        else
        {
            // Usage bar as fallback
            yield return new WidgetDefinition("lcd-usage-bar", hasTemp ? 7 : 8, 2, new
            {
                value = data.usage,
                max = 100,
                label = "CPU Load",
                showPercent = true,
                orientation = "horizontal"
            });
        }
    }

    private static List<object> BuildInfoItems(dynamic data)
    {
        var items = new List<object>();

        if (data.frequency != null)
        {
            items.Add(new { label = "Frequency", value = $"{data.frequency:F0} MHz" });
        }

        if (data.power != null)
        {
            items.Add(new { label = "Power", value = $"{data.power:F1} W" });
        }

        items.Add(new { label = "Cores", value = data.cores.ToString() });

        return items;
    }

    private static string TruncateName(string name, int maxLength)
    {
        if (string.IsNullOrEmpty(name) || name.Length <= maxLength)
            return name;
        return name[..(maxLength - 3)] + "...";
    }
}
