using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Core.Panels;

public sealed class CpuInfoPanel : BaseLivePanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "cpu-info";
    public override string DisplayName => "CPU Info";

    public CpuInfoPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);
        var metrics = await _provider.GetMetricsAsync(cancellationToken);

        image.Mutate(ctx =>
        {
            if (metrics?.Cpu == null || !FontsLoaded)
            {
                DrawCenteredText(ctx, "CPU Data Unavailable", width / 2f, height / 2f - 20, TitleFont!, SecondaryTextColor);
                return;
            }

            var cpu = metrics.Cpu;
            var y = 30;

            DrawText(ctx, "CPU", 30, y, TitleFont!, AccentColor, width - 60);
            y += 50;

            DrawText(ctx, cpu.Name, 30, y, LabelFont!, PrimaryTextColor, width - 60);
            y += 40;

            DrawText(ctx, "Usage:", 30, y, LabelFont!, SecondaryTextColor, 150);
            DrawText(ctx, $"{cpu.UsagePercent:F1}%", 180, y, LabelFont!, GetUsageColor(cpu.UsagePercent), 150);
            y += 35;

            if (cpu.TemperatureCelsius.HasValue)
            {
                DrawText(ctx, "Temp:", 30, y, LabelFont!, SecondaryTextColor, 150);
                DrawText(ctx, $"{cpu.TemperatureCelsius.Value:F0}°C", 180, y, LabelFont!, GetTemperatureColor(cpu.TemperatureCelsius.Value), 150);
                y += 35;
            }

            if (cpu.FrequencyMhz.HasValue)
            {
                DrawText(ctx, "Freq:", 30, y, LabelFont!, SecondaryTextColor, 150);
                DrawText(ctx, $"{cpu.FrequencyMhz.Value:F0} MHz", 180, y, LabelFont!, PrimaryTextColor, 200);
                y += 35;
            }

            if (cpu.PowerWatts.HasValue)
            {
                DrawText(ctx, "Power:", 30, y, LabelFont!, SecondaryTextColor, 150);
                DrawText(ctx, $"{cpu.PowerWatts.Value:F1}W", 180, y, LabelFont!, PrimaryTextColor, 150);
                y += 35;
            }

            DrawText(ctx, "Cores:", 30, y, LabelFont!, SecondaryTextColor, 150);
            DrawText(ctx, cpu.CoreUsages.Count.ToString(), 180, y, LabelFont!, PrimaryTextColor, 100);

            DrawProgressBar(ctx, cpu.UsagePercent, 30, height - 80, width - 60, 30);
            DrawTimestamp(ctx, width, height);
        });

        return image;
    }
}

public sealed class CpuUsageTextPanel : BaseLivePanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "cpu-usage-text";
    public override string DisplayName => "CPU Usage Text";

    public CpuUsageTextPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);
        var metrics = await _provider.GetMetricsAsync(cancellationToken);

        image.Mutate(ctx =>
        {
            if (metrics?.Cpu == null || !FontsLoaded)
            {
                DrawCenteredText(ctx, "--", width / 2f, height / 2f - 40, ValueFont!, SecondaryTextColor);
                return;
            }

            var cpu = metrics.Cpu;

            DrawCenteredText(ctx, "CPU", width / 2f, 40, TitleFont!, AccentColor);
            DrawCenteredText(ctx, $"{cpu.UsagePercent:F0}%", width / 2f, height / 2f - 50, ValueFont!, GetUsageColor(cpu.UsagePercent));

            if (cpu.TemperatureCelsius.HasValue)
            {
                DrawCenteredText(ctx, $"{cpu.TemperatureCelsius.Value:F0}°C", width / 2f, height / 2f + 60, TitleFont!, GetTemperatureColor(cpu.TemperatureCelsius.Value));
            }

            DrawTimestamp(ctx, width, height);
        });

        return image;
    }
}

public sealed class CpuUsageGraphicPanel : BaseLivePanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "cpu-usage-graphic";
    public override string DisplayName => "CPU Usage Graphic";

    public CpuUsageGraphicPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);
        var metrics = await _provider.GetMetricsAsync(cancellationToken);

        image.Mutate(ctx =>
        {
            if (metrics?.Cpu == null) return;

            var cpu = metrics.Cpu;

            if (FontsLoaded) DrawCenteredText(ctx, "CPU", width / 2f, 20, TitleFont!, AccentColor);

            var barY = 70;
            var barHeight = 40;
            DrawProgressBar(ctx, cpu.UsagePercent, 30, barY, width - 60, barHeight);

            if (FontsLoaded) DrawCenteredText(ctx, $"{cpu.UsagePercent:F0}%", width / 2f, barY + 5, LabelFont!, PrimaryTextColor);

            if (cpu.CoreUsages.Count > 0)
            {
                var coreBarY = barY + barHeight + 40;
                var coreBarWidth = Math.Max(20, (width - 60) / cpu.CoreUsages.Count - 4);
                var coreBarHeight = height - coreBarY - 60;
                var startX = 30 + ((width - 60) - (coreBarWidth + 4) * cpu.CoreUsages.Count) / 2;

                for (var i = 0; i < cpu.CoreUsages.Count; i++)
                {
                    var x = startX + i * (coreBarWidth + 4);
                    DrawVerticalBar(ctx, cpu.CoreUsages[i], x, coreBarY, coreBarWidth, coreBarHeight);
                }
            }

            if (cpu.TemperatureCelsius.HasValue && FontsLoaded)
            {
                DrawText(ctx, $"{cpu.TemperatureCelsius.Value:F0}°C", 30, height - 40, LabelFont!, GetTemperatureColor(cpu.TemperatureCelsius.Value), 100);
            }

            DrawTimestamp(ctx, width, height);
        });

        return image;
    }
}
