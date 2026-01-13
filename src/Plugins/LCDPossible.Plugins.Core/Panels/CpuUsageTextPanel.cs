using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;
using LCDPossible.Sdk.Controls;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// Minimalist CPU usage panel showing a giant percentage value.
/// Optimized for at-a-glance monitoring from 3-6 feet away.
/// Uses semantic controls for theme-customizable rendering.
/// </summary>
public sealed class CpuUsageTextPanel : WidgetPanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "cpu-usage-text";
    public override string DisplayName => "CPU Usage Text";

    public CpuUsageTextPanel(ISystemInfoProvider provider)
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
            temperature = cpu?.TemperatureCelsius
        };
    }

    protected override IEnumerable<SemanticControl> DefineControls(object panelData)
    {
        dynamic data = panelData;

        if (!data.hasData)
        {
            yield return new SingleValueControl
            {
                ColSpan = 12,
                RowSpan = 4,
                Label = "CPU",
                Value = "--",
                Size = ValueSize.Hero
            };
            yield break;
        }

        double usage = data.usage;
        double? temp = data.temperature;

        // Layout: Text-only panel with large stat cards
        // 3 columns × 4 rows - all text, no graphics

        // CPU Usage - hero sized for at-a-glance reading
        yield return new SingleValueControl
        {
            ColSpan = 4,
            RowSpan = 4,
            Label = "CPU USAGE",
            Value = $"{usage:F0}",
            Unit = "%",
            Size = ValueSize.Hero,
            Status = usage >= 80 ? StatusLevel.Warning : StatusLevel.Normal
        };

        // Load status - large text indicator
        yield return new SingleValueControl
        {
            ColSpan = 4,
            RowSpan = 4,
            Label = "LOAD",
            Value = usage >= 80 ? "HIGH" : usage >= 50 ? "MED" : "LOW",
            Size = ValueSize.Hero,
            Status = usage >= 80 ? StatusLevel.Warning : usage >= 50 ? StatusLevel.Info : StatusLevel.Success
        };

        // Temperature (if available) or placeholder
        yield return new SingleValueControl
        {
            ColSpan = 4,
            RowSpan = 4,
            Label = "TEMP",
            Value = temp.HasValue ? $"{temp.Value:F0}" : "--",
            Unit = temp.HasValue ? "°C" : null,
            Size = ValueSize.Hero,
            Status = temp.HasValue && temp.Value >= 70 ? StatusLevel.Warning : StatusLevel.Normal
        };
    }
}
