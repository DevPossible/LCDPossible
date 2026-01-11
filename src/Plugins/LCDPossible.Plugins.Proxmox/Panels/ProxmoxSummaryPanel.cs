using LCDPossible.Plugins.Proxmox.Api;
using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Proxmox.Panels;

/// <summary>
/// Panel showing Proxmox cluster summary.
/// </summary>
public sealed class ProxmoxSummaryPanel : BaseLivePanel
{
    private readonly ProxmoxApiClient _client;

    public override string PanelId => "proxmox-summary";
    public override string DisplayName => "Proxmox Summary";

    public ProxmoxSummaryPanel(ProxmoxApiClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await base.InitializeAsync(cancellationToken);
        await _client.InitializeAsync(cancellationToken);
    }

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);
        var metrics = await _client.GetMetricsAsync(cancellationToken);

        image.Mutate(ctx =>
        {
            if (metrics == null || !FontsLoaded)
            {
                DrawCenteredText(ctx, "Proxmox Unavailable", width / 2f, height / 2f - 20, TitleFont!, SecondaryTextColor);
                return;
            }

            // Title
            DrawCenteredText(ctx, $"Proxmox: {metrics.ClusterName}", width / 2f, 20, TitleFont!, AccentColor);

            // Summary boxes
            var boxWidth = (width - 60) / 4;
            var boxY = 80;
            var boxHeight = 100;

            // Nodes
            RenderSummaryBox(ctx, "NODES", $"{metrics.Summary.OnlineNodes}/{metrics.Summary.TotalNodes}",
                metrics.Summary.OnlineNodes == metrics.Summary.TotalNodes ? SuccessColor : WarningColor,
                20, boxY, boxWidth, boxHeight);

            // VMs
            RenderSummaryBox(ctx, "VMs", $"{metrics.Summary.RunningVms}/{metrics.Summary.TotalVms}",
                AccentColor, 30 + boxWidth, boxY, boxWidth, boxHeight);

            // Containers
            RenderSummaryBox(ctx, "LXC", $"{metrics.Summary.RunningContainers}/{metrics.Summary.TotalContainers}",
                SuccessColor, 40 + boxWidth * 2, boxY, boxWidth, boxHeight);

            // Alerts
            var alertCount = metrics.Summary.CriticalAlerts + metrics.Summary.WarningAlerts;
            var alertColor = metrics.Summary.CriticalAlerts > 0 ? CriticalColor :
                             metrics.Summary.WarningAlerts > 0 ? WarningColor : SuccessColor;
            RenderSummaryBox(ctx, "ALERTS", alertCount > 0 ? alertCount.ToString() : "OK",
                alertColor, 50 + boxWidth * 3, boxY, boxWidth, boxHeight);

            // Resource usage bars
            var barY = boxY + boxHeight + 30;
            DrawText(ctx, "CPU:", 20, barY, LabelFont!, SecondaryTextColor, 80);
            DrawProgressBar(ctx, metrics.Summary.CpuUsagePercent, 100, barY, (width - 140) / 2, 25);

            DrawText(ctx, "RAM:", width / 2 + 20, barY, LabelFont!, SecondaryTextColor, 80);
            DrawProgressBar(ctx, metrics.Summary.MemoryUsagePercent, width / 2 + 100, barY, (width - 140) / 2, 25);

            // Recent alert (if any)
            if (metrics.Alerts.Count > 0)
            {
                var alert = metrics.Alerts.OrderByDescending(a => a.Severity).First();
                var color = alert.Severity == AlertSeverity.Critical ? CriticalColor : WarningColor;
                DrawText(ctx, $"⚠ {alert.Title}: {alert.Description}", 20, height - 70, SmallFont!, color, width - 40);
            }

            DrawTimestamp(ctx, width, height);
        });

        return image;
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
