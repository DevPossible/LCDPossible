using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// CPU usage panel with graphical bars showing overall and per-core breakdown.
/// Uses horizontal progress bars optimized for wide 1280x480 displays.
/// </summary>
public sealed class CpuUsageGraphicPanel : WidgetPanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "cpu-usage-graphic";
    public override string DisplayName => "CPU Usage Graphic";

    public CpuUsageGraphicPanel(ISystemInfoProvider provider)
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
            usage = cpu?.UsagePercent ?? 0,
            temperature = cpu?.TemperatureCelsius,
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

        // Top row: Title with value (2 rows for proper sizing) + Overall usage bar
        yield return new WidgetDefinition("lcd-stat-card", 2, 2, new
        {
            title = "CPU",
            value = $"{data.usage:F0}%",
            status = data.usage >= 80 ? "warning" : "",
            size = "large"
        });

        yield return new WidgetDefinition("lcd-usage-bar", 10, 2, new
        {
            value = data.usage,
            max = 100,
            label = "OVERALL",
            showPercent = true
        });

        // Bottom rows: Per-core bars + Temperature (if available)
        if (data.coreUsages.Count > 0)
        {
            // Core usage sparkline
            var coreColSpan = data.temperature != null ? 8 : 10;

            yield return new WidgetDefinition("echarts-sparkline", coreColSpan, 2, new
            {
                values = data.coreUsages,
                label = $"CORE USAGE ({data.coreUsages.Count} CORES)",
                style = "area"
            });

            // Temperature gauge if available
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
                // Info list as fallback
                yield return new WidgetDefinition("lcd-info-list", 2, 2, new
                {
                    items = new object[]
                    {
                        new { label = "LOAD", value = $"{data.usage:F0}%" },
                        new { label = "CORES", value = ((IList<float>)data.coreUsages).Count.ToString() }
                    }
                });
            }
        }
        else
        {
            // No per-core data - show large usage donut instead
            yield return new WidgetDefinition("lcd-donut", 6, 2, new
            {
                value = data.usage,
                max = 100,
                label = "CPU"
            });

            if (data.temperature != null)
            {
                yield return new WidgetDefinition("lcd-temp-gauge", 6, 2, new
                {
                    value = data.temperature,
                    max = 100,
                    label = "TEMP"
                });
            }
            else
            {
                yield return new WidgetDefinition("lcd-stat-card", 6, 2, new
                {
                    title = "CPU LOAD",
                    value = $"{data.usage:F0}%",
                    size = "large"
                });
            }
        }
    }
}
