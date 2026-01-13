using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// Demo panel showing the new ECharts and DaisyUI-based components.
/// Tests all the new graphical controls with real system data.
/// </summary>
public sealed class NewComponentsDemoPanel : WidgetPanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "new-components-demo";
    public override string DisplayName => "New Components Demo";

    public NewComponentsDemoPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    protected override async Task<object> GetPanelDataAsync(CancellationToken cancellationToken)
    {
        var metrics = await _provider.GetMetricsAsync(cancellationToken);
        var cpu = metrics?.Cpu;
        var gpu = metrics?.Gpu;
        var ram = metrics?.Memory;

        return new
        {
            hasData = metrics != null,
            cpuUsage = cpu?.UsagePercent ?? 0,
            cpuTemp = cpu?.TemperatureCelsius ?? 0,
            gpuUsage = gpu?.UsagePercent ?? 0,
            gpuTemp = gpu?.TemperatureCelsius ?? 0,
            ramPercent = ram?.UsagePercent ?? 0,
            // Simulated historical data for sparkline
            cpuHistory = Enumerable.Range(0, 20).Select(_ => Random.Shared.Next(10, 60)).ToArray()
        };
    }

    protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
    {
        dynamic data = panelData;

        // Row 1: ECharts gauge (3 cols), DaisyUI gauge (3 cols), ECharts donut (3 cols), DaisyUI donut (3 cols)

        // ECharts gauge - arc style
        yield return new WidgetDefinition("echarts-gauge", 3, 2, new
        {
            value = data.cpuUsage,
            max = 100,
            label = "CPU",
            unit = "%",
            type = "usage",
            style = "arc"
        });

        // DaisyUI gauge - uses radial-progress
        yield return new WidgetDefinition("daisy-gauge", 3, 2, new
        {
            value = data.gpuUsage,
            max = 100,
            label = "GPU",
            unit = "%",
            type = "usage",
            size = "lg"
        });

        // ECharts donut
        yield return new WidgetDefinition("echarts-donut", 3, 2, new
        {
            value = data.ramPercent,
            max = 100,
            label = "RAM",
            type = "usage"
        });

        // DaisyUI donut (CSS-based)
        yield return new WidgetDefinition("daisy-donut", 3, 2, new
        {
            value = data.cpuUsage,
            max = 100,
            label = "LOAD",
            type = "usage",
            size = "lg"
        });

        // Row 2: ECharts sparkline (6 cols), ECharts progress (3 cols), DaisyUI progress (3 cols)

        // ECharts sparkline
        yield return new WidgetDefinition("echarts-sparkline", 6, 2, new
        {
            values = data.cpuHistory,
            label = "CPU History",
            style = "area"
        });

        // ECharts progress bar
        yield return new WidgetDefinition("echarts-progress", 3, 2, new
        {
            value = data.ramPercent,
            max = 100,
            label = "RAM",
            showPercent = true
        });

        // DaisyUI progress bar
        yield return new WidgetDefinition("daisy-progress", 3, 2, new
        {
            value = data.cpuUsage,
            max = 100,
            label = "CPU",
            showPercent = true,
            type = "usage",
            size = "lg"
        });
    }
}
