using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// GPU info panel showing detailed graphics card information.
/// Displays usage, temperature, VRAM, power, and clock speeds.
/// </summary>
public sealed class GpuInfoPanel : WidgetPanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "gpu-info";
    public override string DisplayName => "GPU Info";

    public GpuInfoPanel(ISystemInfoProvider provider)
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
            name = gpu?.Name ?? "Unknown GPU",
            usage = gpu?.UsagePercent ?? 0,
            temperature = gpu?.TemperatureCelsius,
            memoryUsedMb = gpu?.MemoryUsedMb,
            memoryTotalMb = gpu?.MemoryTotalMb,
            vramPercent,
            power = gpu?.PowerWatts,
            coreClock = gpu?.CoreClockMhz,
            memoryClock = gpu?.MemoryClockMhz,
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
                message = "GPU Data Unavailable"
            });
            yield break;
        }

        // Row 1: GPU name spanning most of width
        yield return new WidgetDefinition("stat", 8, 1, new
        {
            title = "GPU",
            value = TruncateName((string)data.name, 40),
            desc = ""
        });

        // Row 1 right: Temperature if available
        if (data.temperature != null)
        {
            yield return new WidgetDefinition("temp-gauge", 4, 2, new
            {
                value = data.temperature,
                max = 100,
                label = "Temp",
                size = "8rem"
            });
        }
        else
        {
            yield return new WidgetDefinition("stat", 4, 2, new
            {
                title = "Load",
                value = $"{data.usage:F0}%"
            });
        }

        // Row 2: Usage + VRAM bars
        yield return new WidgetDefinition("progress-bar", 4, 1, new
        {
            value = data.usage,
            max = 100,
            label = "Core",
            showPercent = true
        });

        yield return new WidgetDefinition("progress-bar", 4, 1, new
        {
            value = data.vramPercent,
            max = 100,
            label = "VRAM",
            showPercent = true
        });

        // Row 3-4: Info list with details
        var infoItems = new List<object>();

        if (data.memoryUsedMb != null && data.memoryTotalMb != null)
        {
            infoItems.Add(new { label = "VRAM", value = $"{data.memoryUsedMb:F0} / {data.memoryTotalMb:F0} MB" });
        }

        if (data.coreClock != null)
        {
            infoItems.Add(new { label = "Core", value = $"{data.coreClock:F0} MHz" });
        }

        if (data.power != null)
        {
            infoItems.Add(new { label = "Power", value = $"{data.power:F0} W" });
        }

        if (data.fanSpeed != null)
        {
            infoItems.Add(new { label = "Fan", value = $"{data.fanSpeed:F0}%" });
        }

        yield return new WidgetDefinition("info-list", 8, 2, new
        {
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
