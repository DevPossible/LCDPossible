using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using LCDPossible.Sdk;
using LCDPossible.Sdk.Layout;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// Panel displaying network interfaces using the smart widget layout system.
/// Automatically adapts layout based on number of active interfaces (1-4 widgets).
/// </summary>
public sealed class NetworkInfoPanel : SmartLayoutPanel<NetworkInterfaceData>
{
    public override string PanelId => "network-info";
    public override string DisplayName => "Network Info";

    protected override Task<IReadOnlyList<NetworkInterfaceData>> GetItemsAsync(CancellationToken cancellationToken)
    {
        var interfaces = GetActiveNetworkInterfaces();
        return Task.FromResult<IReadOnlyList<NetworkInterfaceData>>(interfaces);
    }

    protected override string GetEmptyStateMessage() => "No active network connections";

    protected override void RenderWidget(
        IImageProcessingContext ctx,
        WidgetRenderContext widget,
        NetworkInterfaceData iface)
    {
        var fonts = widget.Fonts;
        var colors = widget.Colors;

        // Calculate spacing based on widget size
        int lineSpacing = widget.ScaleValue(6);
        int sectionSpacing = widget.ScaleValue(12);
        int y = widget.ContentY;

        // Interface name (title)
        var displayName = TruncateText(iface.Name, 25);
        DrawText(ctx, displayName, widget.ContentX, y, fonts.Title, colors.Accent, widget.ContentWidth);
        y += (int)fonts.Title.Size + lineSpacing;

        // Interface type (Ethernet, WiFi, etc.)
        var typeText = GetInterfaceTypeDisplay(iface.Type);
        DrawText(ctx, typeText, widget.ContentX, y, fonts.Small, colors.TextMuted, widget.ContentWidth);
        y += (int)fonts.Small.Size + sectionSpacing;

        // IP Address - main value
        if (widget.Bounds.Size == WidgetSize.Full)
        {
            // Full widget - large centered IP
            DrawCenteredText(ctx, iface.IpAddress ?? "N/A", widget.ContentCenterX, y + 10, fonts.Value, colors.TextPrimary);
            y += (int)fonts.Value.Size + sectionSpacing + 10;
        }
        else
        {
            // Smaller widgets - left-aligned, smaller font
            DrawText(ctx, iface.IpAddress ?? "N/A", widget.ContentX, y, fonts.Title, colors.TextPrimary, widget.ContentWidth);
            y += (int)fonts.Title.Size + lineSpacing;
        }

        // Subnet mask
        if (!string.IsNullOrEmpty(iface.SubnetMask) && widget.Bounds.Size != WidgetSize.Quarter)
        {
            DrawText(ctx, $"Mask: {iface.SubnetMask}", widget.ContentX, y, fonts.Small, colors.TextSecondary, widget.ContentWidth);
            y += (int)fonts.Small.Size + lineSpacing;
        }

        // Gateway
        if (!string.IsNullOrEmpty(iface.Gateway))
        {
            DrawText(ctx, $"GW: {iface.Gateway}", widget.ContentX, y, fonts.Small, colors.TextSecondary, widget.ContentWidth);
            y += (int)fonts.Small.Size + lineSpacing;
        }

        // Link speed
        if (iface.SpeedMbps > 0)
        {
            var speedText = FormatLinkSpeed(iface.SpeedMbps);
            DrawText(ctx, speedText, widget.ContentX, y, fonts.Small, colors.TextMuted, widget.ContentWidth);
            y += (int)fonts.Small.Size + lineSpacing;
        }

        // MAC address (only show in larger widgets)
        if (!string.IsNullOrEmpty(iface.MacAddress) && widget.Bounds.Size != WidgetSize.Quarter)
        {
            DrawText(ctx, iface.MacAddress, widget.ContentX, y, fonts.Small, colors.TextMuted, widget.ContentWidth);
        }

        // Status indicator (colored dot in top-right)
        DrawStatusIndicator(ctx, widget, true);
    }

    protected override void RenderWidgetBackground(IImageProcessingContext ctx, WidgetRenderContext widget)
    {
        // Draw a subtle border around each widget for visual separation
        var borderRect = new RectangleF(
            widget.Bounds.X + 1,
            widget.Bounds.Y + 1,
            widget.Bounds.Width - 2,
            widget.Bounds.Height - 2);

        ctx.Draw(widget.Colors.BarBorder.WithAlpha(0.3f), 1f, borderRect);
    }

    private void DrawStatusIndicator(IImageProcessingContext ctx, WidgetRenderContext widget, bool isUp)
    {
        int radius = widget.ScaleValue(6);
        int margin = widget.ScaleValue(12);

        var centerX = widget.Bounds.Right - margin;
        var centerY = widget.Bounds.Y + margin;

        var color = isUp ? widget.Colors.Success : widget.Colors.Critical;
        var circle = new EllipsePolygon(centerX, centerY, radius);
        ctx.Fill(color, circle);
    }

    private static string GetInterfaceTypeDisplay(NetworkInterfaceType type) => type switch
    {
        NetworkInterfaceType.Ethernet => "Ethernet",
        NetworkInterfaceType.Wireless80211 => "WiFi",
        NetworkInterfaceType.Loopback => "Loopback",
        NetworkInterfaceType.Tunnel => "VPN/Tunnel",
        NetworkInterfaceType.Ppp => "PPP",
        _ => type.ToString()
    };

    private static string FormatLinkSpeed(long speedMbps)
    {
        if (speedMbps >= 1000)
            return $"{speedMbps / 1000f:F1} Gbps";
        return $"{speedMbps} Mbps";
    }

    private static List<NetworkInterfaceData> GetActiveNetworkInterfaces()
    {
        var result = new List<NetworkInterfaceData>();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                    && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                    && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .OrderByDescending(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                .ThenByDescending(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .ToList();

            foreach (var ni in interfaces)
            {
                var props = ni.GetIPProperties();
                var ipv4 = props.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

                if (ipv4 == null)
                    continue;

                var gateway = props.GatewayAddresses
                    .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);

                result.Add(new NetworkInterfaceData
                {
                    Name = ni.Name,
                    Type = ni.NetworkInterfaceType,
                    IpAddress = ipv4.Address.ToString(),
                    SubnetMask = ipv4.IPv4Mask?.ToString(),
                    Gateway = gateway?.Address.ToString(),
                    MacAddress = FormatMacAddress(ni.GetPhysicalAddress()),
                    SpeedMbps = ni.Speed > 0 ? ni.Speed / 1_000_000 : 0
                });
            }
        }
        catch
        {
            // Network enumeration failed
        }

        return result;
    }

    private static string? FormatMacAddress(PhysicalAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes.Length == 0)
            return null;

        return string.Join(":", bytes.Select(b => b.ToString("X2")));
    }
}

/// <summary>
/// Data for a single network interface.
/// </summary>
public sealed class NetworkInterfaceData
{
    public string Name { get; init; } = string.Empty;
    public NetworkInterfaceType Type { get; init; }
    public string? IpAddress { get; init; }
    public string? SubnetMask { get; init; }
    public string? Gateway { get; init; }
    public string? MacAddress { get; init; }
    public long SpeedMbps { get; init; }
}
