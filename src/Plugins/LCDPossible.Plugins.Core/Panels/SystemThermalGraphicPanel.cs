using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// Combined CPU and GPU thermal display showing both temperatures side by side.
/// Provides quick thermal overview of the entire system.
/// </summary>
public sealed class SystemThermalGraphicPanel : WidgetPanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "system-thermal-graphic";
    public override string DisplayName => "System Thermal";

    public SystemThermalGraphicPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    protected override async Task<object> GetPanelDataAsync(CancellationToken cancellationToken)
    {
        var metrics = await _provider.GetMetricsAsync(cancellationToken);

        return new
        {
            hasData = metrics != null,
            cpuTemp = metrics?.Cpu?.TemperatureCelsius ?? 0,
            cpuUsage = metrics?.Cpu?.UsagePercent ?? 0,
            cpuPower = metrics?.Cpu?.PowerWatts,
            gpuTemp = metrics?.Gpu?.TemperatureCelsius ?? 0,
            gpuUsage = metrics?.Gpu?.UsagePercent ?? 0,
            gpuPower = metrics?.Gpu?.PowerWatts
        };
    }

    protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
    {
        dynamic data = panelData;

        if (!data.hasData)
        {
            yield return WidgetDefinition.FullWidth("empty-state", 4, new
            {
                message = "Thermal Data Unavailable"
            });
            yield break;
        }

        // Layout: 2 equal columns (CPU | GPU), each 6 cols wide
        // Row 1: Temp stat + Load bar (2 rows)
        // Row 2: Large gauge + Info list (2 rows)

        // Row 1: CPU temp + load bar
        yield return new WidgetDefinition("lcd-stat-card", 2, 2, new
        {
            title = "CPU",
            value = $"{data.cpuTemp:F0}°C",
            status = GetTempStatus((float)data.cpuTemp),
            size = "large"
        });

        yield return new WidgetDefinition("lcd-usage-bar", 4, 2, new
        {
            value = data.cpuUsage,
            max = 100,
            label = "LOAD",
            showPercent = true
        });

        // Row 1: GPU temp + load bar
        yield return new WidgetDefinition("lcd-stat-card", 2, 2, new
        {
            title = "GPU",
            value = $"{data.gpuTemp:F0}°C",
            status = GetTempStatus((float)data.gpuTemp),
            size = "large"
        });

        yield return new WidgetDefinition("lcd-usage-bar", 4, 2, new
        {
            value = data.gpuUsage,
            max = 100,
            label = "LOAD",
            showPercent = true
        });

        // Row 2: CPU gauge + info (consistent 3+3 sizing)
        yield return new WidgetDefinition("lcd-temp-gauge", 3, 2, new
        {
            value = data.cpuTemp,
            max = 100,
            label = "CPU"
        });

        var cpuInfo = new List<object>
        {
            new { label = "TEMP", value = $"{data.cpuTemp:F0}°C" },
            new { label = "LOAD", value = $"{data.cpuUsage:F0}%" }
        };
        if (data.cpuPower != null)
        {
            cpuInfo.Add(new { label = "POWER", value = $"{data.cpuPower:F0}W" });
        }

        yield return new WidgetDefinition("lcd-info-list", 3, 2, new
        {
            title = "CPU STATS",
            items = cpuInfo.ToArray()
        });

        // Row 2: GPU gauge + info (consistent 3+3 sizing)
        yield return new WidgetDefinition("lcd-temp-gauge", 3, 2, new
        {
            value = data.gpuTemp,
            max = 100,
            label = "GPU"
        });

        var gpuInfo = new List<object>
        {
            new { label = "TEMP", value = $"{data.gpuTemp:F0}°C" },
            new { label = "LOAD", value = $"{data.gpuUsage:F0}%" }
        };
        if (data.gpuPower != null)
        {
            gpuInfo.Add(new { label = "POWER", value = $"{data.gpuPower:F0}W" });
        }

        yield return new WidgetDefinition("lcd-info-list", 3, 2, new
        {
            title = "GPU STATS",
            items = gpuInfo.ToArray()
        });
    }

    private static string GetTempStatus(float temp) => temp switch
    {
        >= 85 => "critical",
        >= 70 => "warning",
        _ => ""
    };
}
