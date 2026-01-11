using LCDPossible.Core.Monitoring;
using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Core.Panels;

public sealed class RamInfoPanel : BaseLivePanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "ram-info";
    public override string DisplayName => "RAM Info";

    public RamInfoPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);
        var metrics = await _provider.GetMetricsAsync(cancellationToken);

        image.Mutate(ctx =>
        {
            if (metrics?.Memory == null || !FontsLoaded)
            {
                DrawCenteredText(ctx, "RAM Data Unavailable", width / 2f, height / 2f - 20, TitleFont!, SecondaryTextColor);
                return;
            }

            var mem = metrics.Memory;
            var y = 30;

            DrawText(ctx, "MEMORY", 30, y, TitleFont!, AccentColor, width - 60);
            y += 60;

            DrawText(ctx, "Usage:", 30, y, LabelFont!, SecondaryTextColor, 150);
            DrawText(ctx, $"{mem.UsagePercent:F1}%", 180, y, LabelFont!, GetUsageColor(mem.UsagePercent), 150);
            y += 40;

            DrawText(ctx, "Used:", 30, y, LabelFont!, SecondaryTextColor, 150);
            DrawText(ctx, $"{mem.UsedGb:F1} GB", 180, y, LabelFont!, PrimaryTextColor, 200);
            y += 40;

            DrawText(ctx, "Free:", 30, y, LabelFont!, SecondaryTextColor, 150);
            DrawText(ctx, $"{mem.AvailableGb:F1} GB", 180, y, LabelFont!, SuccessColor, 200);
            y += 40;

            DrawText(ctx, "Total:", 30, y, LabelFont!, SecondaryTextColor, 150);
            DrawText(ctx, $"{mem.TotalGb:F1} GB", 180, y, LabelFont!, PrimaryTextColor, 200);

            DrawProgressBar(ctx, mem.UsagePercent, 30, height - 80, width - 60, 30);
            DrawTimestamp(ctx, width, height);
        });

        return image;
    }
}

public sealed class RamUsageTextPanel : BaseLivePanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "ram-usage-text";
    public override string DisplayName => "RAM Usage Text";

    public RamUsageTextPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);
        var metrics = await _provider.GetMetricsAsync(cancellationToken);

        image.Mutate(ctx =>
        {
            if (metrics?.Memory == null || !FontsLoaded)
            {
                DrawCenteredText(ctx, "--", width / 2f, height / 2f - 40, ValueFont!, SecondaryTextColor);
                return;
            }

            var mem = metrics.Memory;

            DrawCenteredText(ctx, "RAM", width / 2f, 40, TitleFont!, AccentColor);
            DrawCenteredText(ctx, $"{mem.UsagePercent:F0}%", width / 2f, height / 2f - 50, ValueFont!, GetUsageColor(mem.UsagePercent));
            DrawCenteredText(ctx, $"{mem.UsedGb:F1} / {mem.TotalGb:F1} GB", width / 2f, height / 2f + 60, TitleFont!, SecondaryTextColor);

            DrawTimestamp(ctx, width, height);
        });

        return image;
    }
}

public sealed class RamUsageGraphicPanel : BaseLivePanel
{
    private readonly ISystemInfoProvider _provider;

    public override string PanelId => "ram-usage-graphic";
    public override string DisplayName => "RAM Usage Graphic";

    public RamUsageGraphicPanel(ISystemInfoProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);
        var metrics = await _provider.GetMetricsAsync(cancellationToken);

        image.Mutate(ctx =>
        {
            if (metrics?.Memory == null) return;

            var mem = metrics.Memory;

            if (FontsLoaded) DrawCenteredText(ctx, "MEMORY", width / 2f, 20, TitleFont!, AccentColor);

            var barWidth = Math.Min(200, width / 3);
            var barHeight = height - 150;
            var barX = (width - barWidth) / 2;
            var barY = 80;

            DrawVerticalBar(ctx, mem.UsagePercent, barX, barY, barWidth, barHeight);

            if (FontsLoaded)
            {
                DrawCenteredText(ctx, $"{mem.UsagePercent:F0}%", width / 2f, barY + barHeight / 2 - 30, ValueFont!, PrimaryTextColor);
                DrawCenteredText(ctx, $"{mem.UsedGb:F1} / {mem.TotalGb:F1} GB", width / 2f, barY + barHeight + 20, LabelFont!, SecondaryTextColor);
            }

            DrawTimestamp(ctx, width, height);
        });

        return image;
    }
}
