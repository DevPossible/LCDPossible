using LCDPossible.Core.Monitoring;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Panels;

/// <summary>
/// Panel showing combined CPU, RAM, GPU information.
/// </summary>
public sealed class BasicInfoPanel : BaseLivePanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "basic-info";
    public override string DisplayName => "Basic Info";

    public BasicInfoPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);
        var metrics = await _provider.GetMetricsAsync(cancellationToken);

        image.Mutate(ctx =>
        {
            if (metrics == null || !FontsLoaded)
            {
                DrawCenteredText(ctx, "System Data Unavailable", width / 2f, height / 2f - 20, TitleFont!, SecondaryTextColor);
                return;
            }

            var colWidth = (width - 40) / 3;
            var barHeight = 35;
            var barY = height - 100;

            // CPU Section
            var cpuX = 20;
            DrawText(ctx, "CPU", cpuX, 20, TitleFont!, AccentColor, colWidth);

            if (metrics.Cpu != null)
            {
                var cpuNameTrunc = TruncateText(metrics.Cpu.Name, 40);
                DrawText(ctx, cpuNameTrunc, cpuX, 70, SmallFont!, SecondaryTextColor, colWidth - 10);

                DrawText(ctx, $"{metrics.Cpu.UsagePercent:F0}%", cpuX, 110, ValueFont!, GetUsageColor(metrics.Cpu.UsagePercent), colWidth);

                if (metrics.Cpu.TemperatureCelsius.HasValue)
                {
                    DrawText(ctx, $"{metrics.Cpu.TemperatureCelsius.Value:F0}°C", cpuX, 190, TitleFont!, GetTemperatureColor(metrics.Cpu.TemperatureCelsius.Value), 100);
                }

                DrawProgressBar(ctx, metrics.Cpu.UsagePercent, cpuX, barY, colWidth - 20, barHeight);
            }

            // RAM Section
            var ramX = 20 + colWidth;
            DrawText(ctx, "RAM", ramX, 20, TitleFont!, AccentColor, colWidth);

            if (metrics.Memory != null)
            {
                DrawText(ctx, $"{metrics.Memory.UsedGb:F1}/{metrics.Memory.TotalGb:F0} GB", ramX, 70, SmallFont!, SecondaryTextColor, colWidth - 10);

                DrawText(ctx, $"{metrics.Memory.UsagePercent:F0}%", ramX, 110, ValueFont!, GetUsageColor(metrics.Memory.UsagePercent), colWidth);

                DrawProgressBar(ctx, metrics.Memory.UsagePercent, ramX, barY, colWidth - 20, barHeight);
            }

            // GPU Section
            var gpuX = 20 + colWidth * 2;
            DrawText(ctx, "GPU", gpuX, 20, TitleFont!, AccentColor, colWidth);

            if (metrics.Gpu != null)
            {
                var gpuNameTrunc = TruncateText(metrics.Gpu.Name, 40);
                DrawText(ctx, gpuNameTrunc, gpuX, 70, SmallFont!, SecondaryTextColor, colWidth - 10);

                DrawText(ctx, $"{metrics.Gpu.UsagePercent:F0}%", gpuX, 110, ValueFont!, GetUsageColor(metrics.Gpu.UsagePercent), colWidth);

                if (metrics.Gpu.TemperatureCelsius.HasValue)
                {
                    DrawText(ctx, $"{metrics.Gpu.TemperatureCelsius.Value:F0}°C", gpuX, 190, TitleFont!, GetTemperatureColor(metrics.Gpu.TemperatureCelsius.Value), 100);
                }

                DrawProgressBar(ctx, metrics.Gpu.UsagePercent, gpuX, barY, colWidth - 20, barHeight);
            }

            DrawTimestamp(ctx, width, height);
        });

        return image;
    }
}

/// <summary>
/// Panel showing CPU, RAM, GPU usage as large text values.
/// </summary>
public sealed class BasicUsageTextPanel : BaseLivePanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "basic-usage-text";
    public override string DisplayName => "Basic Usage Text";

    public BasicUsageTextPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);
        var metrics = await _provider.GetMetricsAsync(cancellationToken);

        image.Mutate(ctx =>
        {
            if (metrics == null || !FontsLoaded)
            {
                DrawCenteredText(ctx, "---", width / 2f, height / 2f - 20, ValueFont!, SecondaryTextColor);
                return;
            }

            var colWidth = width / 3;

            // CPU
            var cpuX = colWidth / 2;
            DrawCenteredText(ctx, "CPU", cpuX, 30, TitleFont!, AccentColor);
            var cpuPercent = metrics.Cpu?.UsagePercent ?? 0;
            DrawCenteredText(ctx, $"{cpuPercent:F0}%", cpuX, height / 2 - 50, ValueFont!, GetUsageColor(cpuPercent));
            if (metrics.Cpu?.TemperatureCelsius.HasValue == true)
            {
                DrawCenteredText(ctx, $"{metrics.Cpu.TemperatureCelsius.Value:F0}°C", cpuX, height / 2 + 50, LabelFont!, GetTemperatureColor(metrics.Cpu.TemperatureCelsius.Value));
            }

            // RAM
            var ramX = colWidth + colWidth / 2;
            DrawCenteredText(ctx, "RAM", ramX, 30, TitleFont!, AccentColor);
            var ramPercent = metrics.Memory?.UsagePercent ?? 0;
            DrawCenteredText(ctx, $"{ramPercent:F0}%", ramX, height / 2 - 50, ValueFont!, GetUsageColor(ramPercent));
            if (metrics.Memory != null)
            {
                DrawCenteredText(ctx, $"{metrics.Memory.UsedGb:F1}GB", ramX, height / 2 + 50, LabelFont!, SecondaryTextColor);
            }

            // GPU
            var gpuX = colWidth * 2 + colWidth / 2;
            DrawCenteredText(ctx, "GPU", gpuX, 30, TitleFont!, AccentColor);
            var gpuPercent = metrics.Gpu?.UsagePercent ?? 0;
            DrawCenteredText(ctx, $"{gpuPercent:F0}%", gpuX, height / 2 - 50, ValueFont!, GetUsageColor(gpuPercent));
            if (metrics.Gpu?.TemperatureCelsius.HasValue == true)
            {
                DrawCenteredText(ctx, $"{metrics.Gpu.TemperatureCelsius.Value:F0}°C", gpuX, height / 2 + 50, LabelFont!, GetTemperatureColor(metrics.Gpu.TemperatureCelsius.Value));
            }

            DrawTimestamp(ctx, width, height);
        });

        return image;
    }
}
