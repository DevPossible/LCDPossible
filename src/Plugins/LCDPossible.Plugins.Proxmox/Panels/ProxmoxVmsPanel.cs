using LCDPossible.Plugins.Proxmox.Api;
using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Proxmox.Panels;

/// <summary>
/// Panel showing Proxmox VM/Container list.
/// </summary>
public sealed class ProxmoxVmsPanel : BaseLivePanel
{
    private readonly ProxmoxApiClient _client;

    public override string PanelId => "proxmox-vms";
    public override string DisplayName => "Proxmox VMs";

    public ProxmoxVmsPanel(ProxmoxApiClient client)
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
                // Show error title
                DrawCenteredText(ctx, "Proxmox Connection Error", width / 2f, height / 2f - 60, TitleFont!, WarningColor);

                // Show specific error message if available
                if (_client.HasSslError)
                {
                    DrawCenteredText(ctx, "SSL Certificate Error", width / 2f, height / 2f - 20, ValueFont!, CriticalColor);
                    DrawCenteredText(ctx, "Run: lcdpossible config set-proxmox --ignore-ssl-errors", width / 2f, height / 2f + 20, SmallFont!, SecondaryTextColor);
                }
                else if (!string.IsNullOrEmpty(_client.LastError))
                {
                    var errorMsg = _client.LastError.Length > 60
                        ? _client.LastError[..57] + "..."
                        : _client.LastError;
                    DrawCenteredText(ctx, errorMsg, width / 2f, height / 2f, SmallFont!, SecondaryTextColor);
                }
                else
                {
                    DrawCenteredText(ctx, "Unable to connect to Proxmox API", width / 2f, height / 2f, SmallFont!, SecondaryTextColor);
                }
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
