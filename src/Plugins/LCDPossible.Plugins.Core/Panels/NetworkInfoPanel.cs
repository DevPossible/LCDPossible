using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// Panel displaying network information including hostname, IP addresses, gateway, etc.
/// </summary>
public sealed class NetworkInfoPanel : BaseLivePanel
{
    public override string PanelId => "network-info";
    public override string DisplayName => "Network Info";

    public override Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = CreateBaseImage(width, height);

        image.Mutate(ctx =>
        {
            if (!FontsLoaded)
            {
                DrawCenteredText(ctx, "Network Info Unavailable", width / 2f, height / 2f, TitleFont!, SecondaryTextColor);
                return;
            }

            var y = 20;
            var leftCol = 20;
            var rightCol = width / 2 + 20;
            var lineHeight = 36;
            var smallLineHeight = 28;

            // Hostname
            DrawText(ctx, "HOSTNAME", leftCol, y, LabelFont!, AccentColor, width - 40);
            y += smallLineHeight;
            DrawText(ctx, Environment.MachineName, leftCol, y, TitleFont!, PrimaryTextColor, width - 40);
            y += lineHeight + 10;

            // Get active network interfaces
            var activeInterfaces = GetActiveNetworkInterfaces();

            if (activeInterfaces.Count == 0)
            {
                DrawText(ctx, "No active network connections", leftCol, y, LabelFont!, WarningColor, width - 40);
            }
            else
            {
                // Show up to 2 interfaces side by side
                var interfaceIndex = 0;
                foreach (var iface in activeInterfaces.Take(2))
                {
                    var col = interfaceIndex == 0 ? leftCol : rightCol;
                    var colWidth = width / 2 - 40;
                    var ifaceY = y;

                    // Interface name
                    var ifaceName = TruncateText(iface.Name, 25);
                    DrawText(ctx, ifaceName, col, ifaceY, LabelFont!, AccentColor, colWidth);
                    ifaceY += smallLineHeight;

                    // IP Address
                    DrawText(ctx, "IP:", col, ifaceY, SmallFont!, SecondaryTextColor, 40);
                    DrawText(ctx, iface.IpAddress ?? "N/A", col + 45, ifaceY, SmallFont!, PrimaryTextColor, colWidth - 45);
                    ifaceY += smallLineHeight;

                    // Subnet
                    DrawText(ctx, "Mask:", col, ifaceY, SmallFont!, SecondaryTextColor, 55);
                    DrawText(ctx, iface.SubnetMask ?? "N/A", col + 60, ifaceY, SmallFont!, PrimaryTextColor, colWidth - 60);
                    ifaceY += smallLineHeight;

                    // Gateway
                    DrawText(ctx, "GW:", col, ifaceY, SmallFont!, SecondaryTextColor, 40);
                    DrawText(ctx, iface.Gateway ?? "N/A", col + 45, ifaceY, SmallFont!, PrimaryTextColor, colWidth - 45);
                    ifaceY += smallLineHeight;

                    // MAC Address
                    DrawText(ctx, "MAC:", col, ifaceY, SmallFont!, SecondaryTextColor, 50);
                    DrawText(ctx, iface.MacAddress ?? "N/A", col + 55, ifaceY, SmallFont!, SecondaryTextColor, colWidth - 55);
                    ifaceY += smallLineHeight;

                    // Speed
                    if (iface.SpeedMbps > 0)
                    {
                        var speedText = iface.SpeedMbps >= 1000
                            ? $"{iface.SpeedMbps / 1000} Gbps"
                            : $"{iface.SpeedMbps} Mbps";
                        DrawText(ctx, speedText, col, ifaceY, SmallFont!, SecondaryTextColor, colWidth);
                    }

                    interfaceIndex++;
                }

                // DNS Servers at the bottom
                var dnsY = height - 80;
                var dnsServers = GetDnsServers();
                if (dnsServers.Count > 0)
                {
                    DrawText(ctx, "DNS:", leftCol, dnsY, SmallFont!, SecondaryTextColor, 50);
                    var dnsText = string.Join(", ", dnsServers.Take(3));
                    DrawText(ctx, dnsText, leftCol + 55, dnsY, SmallFont!, PrimaryTextColor, width - 100);
                }
            }

            DrawTimestamp(ctx, width, height);
        });

        return Task.FromResult(image);
    }

    private static List<NetworkInterfaceInfo> GetActiveNetworkInterfaces()
    {
        var result = new List<NetworkInterfaceInfo>();

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
                {
                    continue;
                }

                var gateway = props.GatewayAddresses
                    .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);

                result.Add(new NetworkInterfaceInfo
                {
                    Name = ni.Name,
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

    private static List<string> GetDnsServers()
    {
        var result = new List<string>();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up);

            foreach (var ni in interfaces)
            {
                var props = ni.GetIPProperties();
                foreach (var dns in props.DnsAddresses)
                {
                    if (dns.AddressFamily == AddressFamily.InterNetwork)
                    {
                        var addr = dns.ToString();
                        if (!result.Contains(addr))
                        {
                            result.Add(addr);
                        }
                    }
                }
            }
        }
        catch
        {
            // DNS enumeration failed
        }

        return result;
    }

    private static string? FormatMacAddress(PhysicalAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes.Length == 0)
        {
            return null;
        }

        return string.Join(":", bytes.Select(b => b.ToString("X2")));
    }

    private sealed class NetworkInterfaceInfo
    {
        public string Name { get; init; } = string.Empty;
        public string? IpAddress { get; init; }
        public string? SubnetMask { get; init; }
        public string? Gateway { get; init; }
        public string? MacAddress { get; init; }
        public long SpeedMbps { get; init; }
    }
}
