using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// Demo panel showing semantic widget types that are resolved by themes.
/// Uses abstract widget names (gauge, donut, sparkline, progress) that
/// the theme system resolves to concrete implementations (echarts-* or daisy-*).
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

        // Row 1: Semantic widgets - theme determines implementation
        // Use "gauge" and "donut" (semantic) - theme picks echarts vs daisy

        // Semantic gauge - theme determines implementation
        yield return new WidgetDefinition("gauge", 3, 2, new
        {
            value = data.cpuUsage,
            max = 100,
            label = "CPU",
            unit = "%",
            type = "usage"
        });

        // Semantic gauge - theme determines implementation
        yield return new WidgetDefinition("gauge", 3, 2, new
        {
            value = data.gpuUsage,
            max = 100,
            label = "GPU",
            unit = "%",
            type = "usage"
        });

        // Semantic donut - theme determines implementation
        yield return new WidgetDefinition("donut", 3, 2, new
        {
            value = data.ramPercent,
            max = 100,
            label = "RAM",
            type = "usage"
        });

        // Semantic donut - theme determines implementation
        yield return new WidgetDefinition("donut", 3, 2, new
        {
            value = data.cpuUsage,
            max = 100,
            label = "LOAD",
            type = "usage"
        });

        // Row 2: Semantic sparkline and progress

        // Semantic sparkline - theme determines implementation
        yield return new WidgetDefinition("sparkline", 6, 2, new
        {
            values = data.cpuHistory,
            label = "CPU History"
        });

        // Semantic progress - theme determines implementation
        yield return new WidgetDefinition("progress", 3, 2, new
        {
            value = data.ramPercent,
            max = 100,
            label = "RAM",
            showPercent = true
        });

        // Semantic progress - theme determines implementation
        yield return new WidgetDefinition("progress", 3, 2, new
        {
            value = data.cpuUsage,
            max = 100,
            label = "CPU",
            showPercent = true,
            type = "usage"
        });
    }
}
