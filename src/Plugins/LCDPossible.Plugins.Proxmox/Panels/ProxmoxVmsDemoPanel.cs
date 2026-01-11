using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Proxmox.Panels;

/// <summary>
/// Demo version of Proxmox VMs panel showing sample data.
/// </summary>
public sealed class ProxmoxVmsDemoPanel : BaseLivePanel
{
    public override string PanelId => "proxmox-vms";
    public override string DisplayName => "Proxmox VMs (Demo)";

    private static readonly (string Type, string Name, string Node, string Status, float Cpu, bool Running)[] DemoVms =
    [
        ("VM", "web-server-01", "pve1", "running", 12.5f, true),
        ("VM", "database-01", "pve1", "running", 45.2f, true),
        ("VM", "app-server-01", "pve2", "running", 23.1f, true),
        ("CT", "dns-server", "pve1", "running", 2.3f, true),
        ("CT", "backup-proxy", "pve3", "running", 5.7f, true),
        ("VM", "dev-machine", "pve2", "stopped", 0f, false),
        ("VM", "test-server", "pve3", "stopped", 0f, false),
        ("CT", "monitoring", "pve1", "running", 8.9f, true),
    ];

    public override Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);

        image.Mutate(ctx =>
        {
            if (!FontsLoaded)
            {
                DrawCenteredText(ctx, "Proxmox VMs Demo", width / 2f, height / 2f - 20, TitleFont!, SecondaryTextColor);
                return;
            }

            // Title
            DrawText(ctx, "VMs & Containers (Demo)", 20, 15, TitleFont!, AccentColor, width - 40);

            // Column headers
            var headerY = 60;
            DrawText(ctx, "Type", 20, headerY, SmallFont!, SecondaryTextColor, 60);
            DrawText(ctx, "Name", 80, headerY, SmallFont!, SecondaryTextColor, 200);
            DrawText(ctx, "Node", 290, headerY, SmallFont!, SecondaryTextColor, 100);
            DrawText(ctx, "Status", 400, headerY, SmallFont!, SecondaryTextColor, 80);
            DrawText(ctx, "CPU", 490, headerY, SmallFont!, SecondaryTextColor, 60);

            var rowY = headerY + 35;
            var rowHeight = 30;
            var maxRows = (height - rowY - 50) / rowHeight;

            foreach (var item in DemoVms.Take(maxRows))
            {
                var typeColor = item.Type == "VM" ? AccentColor : SuccessColor;
                var statusColor = item.Running ? SuccessColor : SecondaryTextColor;

                DrawText(ctx, item.Type, 20, rowY, SmallFont!, typeColor, 60);
                DrawText(ctx, TruncateName(item.Name, 18), 80, rowY, SmallFont!, PrimaryTextColor, 200);
                DrawText(ctx, item.Node, 290, rowY, SmallFont!, SecondaryTextColor, 100);
                DrawText(ctx, item.Status, 400, rowY, SmallFont!, statusColor, 80);

                if (item.Running)
                {
                    DrawText(ctx, $"{item.Cpu:F0}%", 490, rowY, SmallFont!, GetUsageColor(item.Cpu), 60);
                }

                rowY += rowHeight;
            }

            DrawTimestamp(ctx, width, height);
        });

        return Task.FromResult(image);
    }

    private static string TruncateName(string name, int maxLength)
    {
        return name.Length > maxLength ? name[..(maxLength - 3)] + "..." : name;
    }
}
