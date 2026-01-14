using System.Net.NetworkInformation;
using System.Net.Sockets;
using LCDPossible.Sdk;

namespace LCDPossible.Plugins.Core.Panels;

/// <summary>
/// Network info panel using the WidgetPanel system.
/// Displays active network interfaces using HTML/CSS web components.
/// Automatically adapts layout based on number of interfaces (1-4).
/// </summary>
public sealed class NetworkWidgetPanel : WidgetPanel
{
    public override string PanelId => "network-info";
    public override string DisplayName => "Network Info";

    protected override Task<object> GetPanelDataAsync(CancellationToken cancellationToken)
    {
        // Panel-level data (not much needed since items are the focus)
        return Task.FromResult<object>(new
        {
            timestamp = DateTime.Now.ToString("HH:mm:ss")
        });
    }

    protected override Task<IReadOnlyList<object>> GetItemsAsync(CancellationToken cancellationToken)
    {
        var interfaces = GetActiveNetworkInterfaces();
        return Task.FromResult<IReadOnlyList<object>>(interfaces.Cast<object>().ToList());
    }

    protected override string GetEmptyStateMessage() => "No active network connections";

    protected override IEnumerable<WidgetDefinition> DefineWidgets(object panelData)
    {
        // No static widgets - all widgets come from items
        yield break;
    }

    protected override WidgetDefinition? DefineItemWidget(object item, int index, int totalItems)
    {
        var iface = (NetworkInterfaceInfo)item;

        // Calculate layout based on item count
        var (colSpan, rowSpan) = WidgetLayouts.GetAutoLayout(index, totalItems);

        // Build info items list
        var infoItems = new List<object>
        {
            new { label = "IP", value = iface.IpAddress ?? "N/A", color = "var(--color-text-primary)" }
        };

        // Add optional items based on available space
        if (totalItems <= 2)
        {
            // More space - show more details
            if (!string.IsNullOrEmpty(iface.SubnetMask))
            {
                infoItems.Add(new { label = "Mask", value = iface.SubnetMask });
            }

            if (!string.IsNullOrEmpty(iface.Gateway))
            {
                infoItems.Add(new { label = "Gateway", value = iface.Gateway });
            }

            if (iface.SpeedMbps > 0)
            {
                infoItems.Add(new { label = "Speed", value = FormatLinkSpeed(iface.SpeedMbps) });
            }

            if (!string.IsNullOrEmpty(iface.MacAddress))
            {
                infoItems.Add(new { label = "MAC", value = iface.MacAddress });
            }
        }
        else
        {
            // Less space - show minimal info
            if (!string.IsNullOrEmpty(iface.Gateway))
            {
                infoItems.Add(new { label = "GW", value = iface.Gateway });
            }

            if (iface.SpeedMbps > 0)
            {
                infoItems.Add(new { label = "Speed", value = FormatLinkSpeed(iface.SpeedMbps) });
            }
        }

        // Use lcd-info-list for each interface with a title
        return new WidgetDefinition("lcd-info-list", colSpan, rowSpan, new
        {
            title = iface.Name,
            subtitle = GetInterfaceTypeDisplay(iface.Type),
            items = infoItems,
            status = "success" // Interface is up
        });
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
            return $"{speedMbps / 1000.0:F1} Gbps";
        return $"{speedMbps} Mbps";
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
                    continue;

                var gateway = props.GatewayAddresses
                    .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);

                result.Add(new NetworkInterfaceInfo
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
/// Data for a single network interface (Widget version).
/// </summary>
internal sealed class NetworkInterfaceInfo
{
    public string Name { get; init; } = string.Empty;
    public NetworkInterfaceType Type { get; init; }
    public string? IpAddress { get; init; }
    public string? SubnetMask { get; init; }
    public string? Gateway { get; init; }
    public string? MacAddress { get; init; }
    public long SpeedMbps { get; init; }
}
