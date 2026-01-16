using System.Net;
using System.Net.Sockets;

namespace LCDPossible.Core.Usb.Discovery;

/// <summary>
/// Client for discovering VirtualLCD instances on the network.
/// </summary>
public sealed class VirtualDeviceDiscoveryClient : IDisposable
{
    private readonly UdpClient _client;
    private bool _disposed;

    /// <summary>
    /// Creates a new discovery client.
    /// </summary>
    public VirtualDeviceDiscoveryClient()
    {
        _client = new UdpClient
        {
            EnableBroadcast = true
        };
        _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
    }

    /// <summary>
    /// Discover all VirtualLCD instances on the local network.
    /// </summary>
    /// <param name="timeout">Discovery timeout (default 500ms).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of discovered devices.</returns>
    public async Task<IReadOnlyList<DiscoveredVirtualDevice>> DiscoverAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        timeout ??= VirtualDeviceDiscovery.DefaultTimeout;
        var discovered = new List<DiscoveredVirtualDevice>();

        // Send discovery request to broadcast address
        var request = new DiscoveryRequest();
        var requestData = request.ToBytes();

        var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, VirtualDeviceDiscovery.DiscoveryPort);

        try
        {
            await _client.SendAsync(requestData, broadcastEndpoint, cancellationToken);
        }
        catch (SocketException)
        {
            // Broadcast may not be available, try localhost only
            var localhostEndpoint = new IPEndPoint(IPAddress.Loopback, VirtualDeviceDiscovery.DiscoveryPort);
            await _client.SendAsync(requestData, localhostEndpoint, cancellationToken);
        }

        // Collect responses until timeout
        using var timeoutCts = new CancellationTokenSource(timeout.Value);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var seenInstances = new HashSet<string>(); // Prevent duplicates

        while (!linkedCts.Token.IsCancellationRequested)
        {
            try
            {
                var result = await _client.ReceiveAsync(linkedCts.Token);
                var response = DiscoveryResponse.FromBytes(result.Buffer);

                if (response?.IsValid() == true)
                {
                    var instanceKey = $"{result.RemoteEndPoint.Address}:{response.DevicePort}";

                    if (seenInstances.Add(instanceKey))
                    {
                        discovered.Add(new DiscoveredVirtualDevice
                        {
                            Host = result.RemoteEndPoint.Address.ToString(),
                            Port = response.DevicePort,
                            InstanceName = response.InstanceName,
                            ProtocolId = response.ProtocolId,
                            Width = response.Width,
                            Height = response.Height,
                            VendorId = response.VendorId,
                            ProductId = response.ProductId,
                            HidReportSize = response.HidReportSize
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Timeout reached, stop collecting
                break;
            }
            catch (SocketException)
            {
                // Socket error, stop collecting
                break;
            }
        }

        // Sort by instance name for consistent ordering (virtual devices listed first alphabetically)
        return discovered
            .OrderBy(d => d.InstanceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _client.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Information about a discovered VirtualLCD instance.
/// </summary>
public sealed class DiscoveredVirtualDevice
{
    /// <summary>
    /// Host address where the device is running.
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// UDP port for HID packet communication.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Instance name (for display).
    /// </summary>
    public required string InstanceName { get; init; }

    /// <summary>
    /// Protocol being simulated.
    /// </summary>
    public required string ProtocolId { get; init; }

    /// <summary>
    /// Display width.
    /// </summary>
    public required int Width { get; init; }

    /// <summary>
    /// Display height.
    /// </summary>
    public required int Height { get; init; }

    /// <summary>
    /// Emulated USB Vendor ID.
    /// </summary>
    public required ushort VendorId { get; init; }

    /// <summary>
    /// Emulated USB Product ID.
    /// </summary>
    public required ushort ProductId { get; init; }

    /// <summary>
    /// HID report size.
    /// </summary>
    public required int HidReportSize { get; init; }

    /// <summary>
    /// Convert to VirtualDeviceConfig for use with VirtualDeviceEnumerator.
    /// </summary>
    public VirtualDeviceConfig ToConfig() => new()
    {
        Host = Host,
        Port = Port,
        VendorId = VendorId,
        ProductId = ProductId,
        Name = InstanceName,
        MaxOutputReportLength = HidReportSize
    };
}
