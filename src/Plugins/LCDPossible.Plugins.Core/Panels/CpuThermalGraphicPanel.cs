using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// CPU thermal display with temperature gauge and related metrics.
/// Shows temperature prominently with power and load info.
/// </summary>
public sealed class CpuThermalGraphicPanel : WidgetPanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "cpu-thermal-graphic";
    public override string DisplayName => "CPU Thermal";

    public CpuThermalGraphicPanel(ISystemInfoProvider provider)
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
            name = TruncateName(cpu?.Name ?? "Unknown CPU", 40),
            temperature = cpu?.TemperatureCelsius ?? 0,
            usage = cpu?.UsagePercent ?? 0,
            power = cpu?.PowerWatts
        };
    }

    protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
    {
        dynamic data = panelData;

        if (!data.hasData)
        {
            yield return WidgetDefinition.FullWidth("empty-state", 4, new
            {
                message = "CPU Thermal Data Unavailable"
            });
            yield break;
        }

        // Layout: Left side (6 cols) = CPU name + status info
        //         Right side (6 cols) = Large temperature gauge

        // Left column: CPU name card (full height for impact)
        yield return new WidgetDefinition("lcd-stat-card", 6, 2, new
        {
            title = "CPU THERMAL",
            value = (string)data.name,
            size = "medium"
        });

        // Right column: Large temperature gauge (4 rows for maximum size)
        yield return new WidgetDefinition("lcd-temp-gauge", 6, 4, new
        {
            value = data.temperature,
            max = 100,
            label = "TEMPERATURE"
        });

        // Left column: Status info list
        var infoItems = new List<object>
        {
            new { label = "LOAD", value = $"{data.usage:F0}%" }
        };

        if (data.power != null)
        {
            infoItems.Add(new { label = "POWER", value = $"{data.power:F1} W" });
        }

        yield return new WidgetDefinition("lcd-info-list", 6, 2, new
        {
            title = "STATUS",
            items = infoItems.ToArray()
        });
    }

    private static string TruncateName(string name, int maxLength)
    {
        if (string.IsNullOrEmpty(name) || name.Length <= maxLength)
            return name;
        return name[..(maxLength - 3)] + "...";
    }
}
