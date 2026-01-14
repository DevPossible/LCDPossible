using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// GPU usage panel with graphical bars for core and VRAM usage.
/// Shows temperature and power info when available.
/// </summary>
public sealed class GpuUsageGraphicPanel : WidgetPanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "gpu-usage-graphic";
    public override string DisplayName => "GPU Usage Graphic";

    public GpuUsageGraphicPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    protected override async Task<object> GetPanelDataAsync(CancellationToken cancellationToken)
    {
        var metrics = await _provider.GetMetricsAsync(cancellationToken);
        var gpu = metrics?.Gpu;

        var vramPercent = 0f;
        if (gpu?.MemoryUsagePercent.HasValue == true)
        {
            vramPercent = gpu.MemoryUsagePercent.Value;
        }
        else if (gpu?.MemoryTotalMb.HasValue == true && gpu?.MemoryUsedMb.HasValue == true && gpu.MemoryTotalMb.Value > 0)
        {
            vramPercent = gpu.MemoryUsedMb.Value / gpu.MemoryTotalMb.Value * 100f;
        }

        return new
        {
            hasData = gpu != null,
            usage = gpu?.UsagePercent ?? 0,
            vramPercent,
            temperature = gpu?.TemperatureCelsius,
            power = gpu?.PowerWatts
        };
    }

    protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
    {
        dynamic data = panelData;

        if (!data.hasData)
        {
            yield return WidgetDefinition.FullWidth("empty-state", 4, new
            {
                message = "GPU Data Unavailable"
            });
            yield break;
        }

        // Layout: 2 rows
        // Row 1: GPU stat | CORE bar | Info list
        // Row 2: VRAM stat | VRAM bar | Temp gauge (moved down)

        // Row 1: GPU stat card (left)
        yield return new WidgetDefinition("lcd-stat-card", 3, 2, new
        {
            title = "GPU",
            value = $"{data.usage:F0}%",
            status = data.usage >= 80 ? "warning" : "",
            size = "large"
        });

        // Row 1: CORE usage bar (center)
        yield return new WidgetDefinition("lcd-usage-bar", 5, 2, new
        {
            value = data.usage,
            max = 100,
            label = "CORE",
            showPercent = true
        });

        // Row 1: Info list (right top)
        if (data.temperature != null)
        {
            var infoItems = new List<object>
            {
                new { label = "LOAD", value = $"{data.usage:F0}%" }
            };

            if (data.power != null)
            {
                infoItems.Add(new { label = "POWER", value = $"{data.power:F0} W" });
            }

            yield return new WidgetDefinition("lcd-info-list", 4, 2, new
            {
                title = "STATUS",
                items = infoItems.ToArray()
            });
        }
        else
        {
            // No temperature - show power info if available
            yield return new WidgetDefinition("lcd-stat-card", 4, 2, new
            {
                title = "POWER",
                value = data.power != null ? $"{data.power:F0} W" : "--",
                size = "large"
            });
        }

        // Row 2: VRAM stat card (left)
        yield return new WidgetDefinition("lcd-stat-card", 3, 2, new
        {
            title = "VRAM",
            value = $"{data.vramPercent:F0}%",
            status = data.vramPercent >= 80 ? "warning" : "",
            size = "large"
        });

        // Row 2: VRAM usage bar (center, under CORE)
        yield return new WidgetDefinition("lcd-usage-bar", 5, 2, new
        {
            value = data.vramPercent,
            max = 100,
            label = "VRAM",
            showPercent = true
        });

        // Row 2: Temp gauge (right bottom - moved down)
        if (data.temperature != null)
        {
            yield return new WidgetDefinition("lcd-temp-gauge", 4, 2, new
            {
                value = data.temperature,
                max = 100,
                label = "TEMP"
            });
        }
        else
        {
            // No temperature - show VRAM info
            yield return new WidgetDefinition("lcd-stat-card", 4, 2, new
            {
                title = "VRAM LOAD",
                value = $"{data.vramPercent:F0}%",
                size = "large"
            });
        }
    }
}
