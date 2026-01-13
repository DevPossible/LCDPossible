using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// GPU thermal display with temperature gauge and related metrics.
/// Shows temperature prominently with power, fan, and load info.
/// </summary>
public sealed class GpuThermalGraphicPanel : WidgetPanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "gpu-thermal-graphic";
    public override string DisplayName => "GPU Thermal";

    public GpuThermalGraphicPanel(ISystemInfoProvider provider)
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
            name = TruncateName(gpu?.Name ?? "Unknown GPU", 50),
            temperature = gpu?.TemperatureCelsius ?? 0,
            usage = gpu?.UsagePercent ?? 0,
            power = gpu?.PowerWatts,
            fanSpeed = gpu?.FanSpeedPercent
        };
    }

    protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
    {
        dynamic data = panelData;

        if (!data.hasData)
        {
            yield return WidgetDefinition.FullWidth("empty-state", 4, new
            {
                message = "GPU Thermal Data Unavailable"
            });
            yield break;
        }

        // Layout: Left side (6 cols) = GPU name + status info
        //         Right side (6 cols) = Large temperature gauge

        // Left column: GPU name card
        yield return new WidgetDefinition("lcd-stat-card", 6, 2, new
        {
            title = "GPU THERMAL",
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
            infoItems.Add(new { label = "POWER", value = $"{data.power:F0} W" });
        }

        if (data.fanSpeed != null)
        {
            infoItems.Add(new { label = "FAN", value = $"{data.fanSpeed:F0}%" });
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
