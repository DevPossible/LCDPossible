using LCDPossible.Core.Monitoring;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Panels;

/// <summary>
/// Panel showing Proxmox cluster summary.
/// </summary>
public sealed class ProxmoxSummaryPanel : BaseLivePanel
{
    private readonly IProxmoxProvider _provider;

    public override string PanelId => "proxmox-summary";
    public override string DisplayName => "Proxmox Summary";

    public ProxmoxSummaryPanel(IProxmoxProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);
        var metrics = await _provider.GetProxmoxMetricsAsync(cancellationToken);

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

/// <summary>
/// Panel showing Proxmox VM/Container list.
/// </summary>
public sealed class ProxmoxVmsPanel : BaseLivePanel
{
    private readonly IProxmoxProvider _provider;

    public override string PanelId => "proxmox-vms";
    public override string DisplayName => "Proxmox VMs";

    public ProxmoxVmsPanel(IProxmoxProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public override async Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);
        var metrics = await _provider.GetProxmoxMetricsAsync(cancellationToken);

        image.Mutate(ctx =>
        {
            if (metrics == null || !FontsLoaded)
            {
                DrawCenteredText(ctx, "Proxmox Unavailable", width / 2f, height / 2f - 20, TitleFont!, SecondaryTextColor);
                return;
            }

            // Title
            DrawText(ctx, "VMs & Containers", 20, 15, TitleFont!, AccentColor, width - 40);

            // Column headers
            var headerY = 60;
            DrawText(ctx, "Type", 20, headerY, SmallFont!, SecondaryTextColor, 60);
            DrawText(ctx, "Name", 80, headerY, SmallFont!, SecondaryTextColor, 200);
            DrawText(ctx, "Node", 290, headerY, SmallFont!, SecondaryTextColor, 100);
            DrawText(ctx, "Status", 400, headerY, SmallFont!, SecondaryTextColor, 80);
            DrawText(ctx, "CPU", 490, headerY, SmallFont!, SecondaryTextColor, 60);

            // Combine and sort VMs and containers
            var items = metrics.VirtualMachines
                .Select(v => new { Type = "VM", v.Name, v.Node, v.Status, v.CpuUsagePercent, v.IsRunning })
                .Concat(metrics.Containers
                    .Select(c => new { Type = "CT", c.Name, c.Node, c.Status, c.CpuUsagePercent, c.IsRunning }))
                .OrderByDescending(x => x.IsRunning)
                .ThenBy(x => x.Type)
                .ThenBy(x => x.Name)
                .ToList();

            var rowY = headerY + 35;
            var rowHeight = 30;
            var maxRows = (height - rowY - 50) / rowHeight;

            foreach (var item in items.Take(maxRows))
            {
                var typeColor = item.Type == "VM" ? AccentColor : SuccessColor;
                var statusColor = item.IsRunning ? SuccessColor : SecondaryTextColor;

                DrawText(ctx, item.Type, 20, rowY, SmallFont!, typeColor, 60);
                DrawText(ctx, TruncateName(item.Name, 18), 80, rowY, SmallFont!, PrimaryTextColor, 200);
                DrawText(ctx, item.Node, 290, rowY, SmallFont!, SecondaryTextColor, 100);
                DrawText(ctx, item.Status, 400, rowY, SmallFont!, statusColor, 80);

                if (item.IsRunning)
                {
                    DrawText(ctx, $"{item.CpuUsagePercent:F0}%", 490, rowY, SmallFont!, GetUsageColor(item.CpuUsagePercent), 60);
                }

                rowY += rowHeight;
            }

            if (items.Count > maxRows)
            {
                DrawText(ctx, $"... and {items.Count - maxRows} more", 20, height - 45, SmallFont!, SecondaryTextColor, width - 40);
            }

            DrawTimestamp(ctx, width, height);
        });

        return image;
    }

    private static string TruncateName(string name, int maxLength)
    {
        return name.Length > maxLength ? name[..(maxLength - 3)] + "..." : name;
    }
}
