using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Proxmox.Panels;

/// <summary>
/// Demo version of Proxmox summary panel showing sample data.
/// </summary>
public sealed class ProxmoxSummaryDemoPanel : BaseLivePanel
{
    public override string PanelId => "proxmox-summary";
    public override string DisplayName => "Proxmox Summary (Demo)";

    public override Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);

        image.Mutate(ctx =>
        {
            if (!FontsLoaded)
            {
                DrawCenteredText(ctx, "Proxmox Demo", width / 2f, height / 2f - 20, TitleFont!, SecondaryTextColor);
                return;
            }

            // Title
            DrawCenteredText(ctx, "Proxmox: demo-cluster (Demo)", width / 2f, 20, TitleFont!, AccentColor);

            // Summary boxes
            var boxWidth = (width - 60) / 4;
            var boxY = 80;
            var boxHeight = 100;

            // Nodes
            RenderSummaryBox(ctx, "NODES", "3/3", SuccessColor, 20, boxY, boxWidth, boxHeight);

            // VMs
            RenderSummaryBox(ctx, "VMs", "12/15", AccentColor, 30 + boxWidth, boxY, boxWidth, boxHeight);

            // Containers
            RenderSummaryBox(ctx, "LXC", "8/10", SuccessColor, 40 + boxWidth * 2, boxY, boxWidth, boxHeight);

            // Alerts
            RenderSummaryBox(ctx, "ALERTS", "OK", SuccessColor, 50 + boxWidth * 3, boxY, boxWidth, boxHeight);

            // Resource usage bars
            var barY = boxY + boxHeight + 30;
            DrawText(ctx, "CPU:", 20, barY, LabelFont!, SecondaryTextColor, 80);
            DrawProgressBar(ctx, 45.5f, 100, barY, (width - 140) / 2, 25);

            DrawText(ctx, "RAM:", width / 2 + 20, barY, LabelFont!, SecondaryTextColor, 80);
            DrawProgressBar(ctx, 67.3f, width / 2 + 100, barY, (width - 140) / 2, 25);

            // Demo notice
            DrawText(ctx, "Demo mode - configure Proxmox API for live data", 20, height - 70, SmallFont!, SecondaryTextColor, width - 40);

            DrawTimestamp(ctx, width, height);
        });

        return Task.FromResult(image);
    }

    private void RenderSummaryBox(IImageProcessingContext ctx, string label, string value, Color valueColor, int x, int y, int width, int height)
    {
        // Box background
        ctx.Fill(Color.FromRgba(40, 40, 50, 200), new RectangleF(x, y, width, height));
        ctx.Draw(Color.FromRgb(60, 60, 70), 2f, new RectangleF(x, y, width, height));

        // Label
        if (FontsLoaded && LabelFont != null)
        {
            DrawCenteredText(ctx, label, x + width / 2f, y + 10, SmallFont!, SecondaryTextColor);
        }

        // Value
        if (FontsLoaded && TitleFont != null)
        {
            DrawCenteredText(ctx, value, x + width / 2f, y + 40, TitleFont!, valueColor);
        }
    }
}
