using LCDPossible.Core.Monitoring;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Panels;

/// <summary>
/// Panel showing detailed GPU information.
/// </summary>
public sealed class GpuInfoPanel : BaseLivePanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "gpu-info";
    public override string DisplayName => "GPU Info";

    public GpuInfoPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);
        var metrics = await _provider.GetMetricsAsync(cancellationToken);

        image.Mutate(ctx =>
        {
            if (metrics?.Gpu == null || !FontsLoaded)
            {
                DrawCenteredText(ctx, "GPU Data Unavailable", width / 2f, height / 2f - 20, TitleFont!, SecondaryTextColor);
                return;
            }

            var gpu = metrics.Gpu;
            var y = 30;

            // Title
            DrawText(ctx, "GPU", 30, y, TitleFont!, AccentColor, width - 60);
            y += 50;

            // GPU Name
            DrawText(ctx, gpu.Name, 30, y, LabelFont!, PrimaryTextColor, width - 60);
            y += 40;

            // Usage
            DrawText(ctx, "Usage:", 30, y, LabelFont!, SecondaryTextColor, 150);
            DrawText(ctx, $"{gpu.UsagePercent:F1}%", 180, y, LabelFont!, GetUsageColor(gpu.UsagePercent), 150);
            y += 35;

            // Temperature
            if (gpu.TemperatureCelsius.HasValue)
            {
                DrawText(ctx, "Temp:", 30, y, LabelFont!, SecondaryTextColor, 150);
                DrawText(ctx, $"{gpu.TemperatureCelsius.Value:F0}°C", 180, y, LabelFont!, GetTemperatureColor(gpu.TemperatureCelsius.Value), 150);
                y += 35;
            }

            // VRAM
            if (gpu.MemoryUsedMb.HasValue && gpu.MemoryTotalMb.HasValue)
            {
                DrawText(ctx, "VRAM:", 30, y, LabelFont!, SecondaryTextColor, 150);
                DrawText(ctx, $"{gpu.MemoryUsedMb.Value:F0} / {gpu.MemoryTotalMb.Value:F0} MB", 180, y, LabelFont!, PrimaryTextColor, 300);
                y += 35;
            }

            // Power
            if (gpu.PowerWatts.HasValue)
            {
                DrawText(ctx, "Power:", 30, y, LabelFont!, SecondaryTextColor, 150);
                DrawText(ctx, $"{gpu.PowerWatts.Value:F1}W", 180, y, LabelFont!, PrimaryTextColor, 150);
                y += 35;
            }

            // Core clock
            if (gpu.CoreClockMhz.HasValue)
            {
                DrawText(ctx, "Core:", 30, y, LabelFont!, SecondaryTextColor, 150);
                DrawText(ctx, $"{gpu.CoreClockMhz.Value:F0} MHz", 180, y, LabelFont!, PrimaryTextColor, 200);
            }

            // Progress bar for usage
            DrawProgressBar(ctx, gpu.UsagePercent, 30, height - 80, width - 60, 30);

            DrawTimestamp(ctx, width, height);
        });

        return image;
    }
}

/// <summary>
/// Panel showing GPU usage as large text.
/// </summary>
public sealed class GpuUsageTextPanel : BaseLivePanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "gpu-usage-text";
    public override string DisplayName => "GPU Usage Text";

    public GpuUsageTextPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);
        var metrics = await _provider.GetMetricsAsync(cancellationToken);

        image.Mutate(ctx =>
        {
            if (metrics?.Gpu == null || !FontsLoaded)
            {
                DrawCenteredText(ctx, "--", width / 2f, height / 2f - 40, ValueFont!, SecondaryTextColor);
                return;
            }

            var gpu = metrics.Gpu;

            // Label
            DrawCenteredText(ctx, "GPU", width / 2f, 40, TitleFont!, AccentColor);

            // Large percentage
            DrawCenteredText(ctx, $"{gpu.UsagePercent:F0}%", width / 2f, height / 2f - 50, ValueFont!, GetUsageColor(gpu.UsagePercent));

            // Temperature below
            if (gpu.TemperatureCelsius.HasValue)
            {
                DrawCenteredText(ctx, $"{gpu.TemperatureCelsius.Value:F0}°C", width / 2f, height / 2f + 60, TitleFont!, GetTemperatureColor(gpu.TemperatureCelsius.Value));
            }

            DrawTimestamp(ctx, width, height);
        });

        return image;
    }
}

/// <summary>
/// Panel showing GPU usage as graphical bars.
/// </summary>
public sealed class GpuUsageGraphicPanel : BaseLivePanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "gpu-usage-graphic";
    public override string DisplayName => "GPU Usage Graphic";

    public GpuUsageGraphicPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);
        var metrics = await _provider.GetMetricsAsync(cancellationToken);

        image.Mutate(ctx =>
        {
            if (metrics?.Gpu == null)
            {
                return;
            }

            var gpu = metrics.Gpu;

            // Title
            if (FontsLoaded)
            {
                DrawCenteredText(ctx, "GPU", width / 2f, 20, TitleFont!, AccentColor);
            }

            // Core usage bar
            var barY = 70;
            var barHeight = 50;
            DrawProgressBar(ctx, gpu.UsagePercent, 30, barY, width - 60, barHeight);

            if (FontsLoaded)
            {
                DrawText(ctx, "Core", 35, barY + 12, LabelFont!, PrimaryTextColor, 100);
                DrawText(ctx, $"{gpu.UsagePercent:F0}%", width - 130, barY + 12, LabelFont!, PrimaryTextColor, 90);
            }

            // VRAM usage bar
            barY += barHeight + 30;
            var vramPercent = gpu.MemoryUsagePercent ??
                              (gpu.MemoryTotalMb.HasValue && gpu.MemoryUsedMb.HasValue
                                  ? (gpu.MemoryUsedMb.Value / gpu.MemoryTotalMb.Value * 100)
                                  : 0);

            DrawProgressBar(ctx, vramPercent, 30, barY, width - 60, barHeight);

            if (FontsLoaded)
            {
                DrawText(ctx, "VRAM", 35, barY + 12, LabelFont!, PrimaryTextColor, 100);
                DrawText(ctx, $"{vramPercent:F0}%", width - 130, barY + 12, LabelFont!, PrimaryTextColor, 90);
            }

            // Temperature and power at bottom
            var infoY = barY + barHeight + 40;
            if (FontsLoaded)
            {
                if (gpu.TemperatureCelsius.HasValue)
                {
                    DrawText(ctx, $"Temp: {gpu.TemperatureCelsius.Value:F0}°C", 30, infoY, LabelFont!, GetTemperatureColor(gpu.TemperatureCelsius.Value), 200);
                }

                if (gpu.PowerWatts.HasValue)
                {
                    DrawText(ctx, $"Power: {gpu.PowerWatts.Value:F0}W", width / 2, infoY, LabelFont!, SecondaryTextColor, 200);
                }
            }

            DrawTimestamp(ctx, width, height);
        });

        return image;
    }
}
