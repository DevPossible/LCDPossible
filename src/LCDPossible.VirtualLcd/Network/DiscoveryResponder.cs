using System.Net;
using System.Net.Sockets;
using LCDPossible.VirtualLcd.Protocols;

namespace LCDPossible.VirtualLcd.Network;

/// <summary>
/// Listens for discovery requests and responds with device information.
/// </summary>
public sealed class DiscoveryResponder : IDisposable
{
    private readonly UdpClient _listener;
    private readonly DiscoveryResponse _deviceInfo;
    private readonly CancellationTokenSource _cts = new();
    private Task? _listenTask;
    private bool _disposed;

    /// <summary>
    /// Creates a new discovery responder.
    /// </summary>
    /// <param name="protocol">The LCD protocol being simulated.</param>
    /// <param name="devicePort">The UDP port where HID packets are received.</param>
    /// <param name="instanceName">Optional instance name (defaults to protocol display name).</param>
    public DiscoveryResponder(ILcdProtocol protocol, int devicePort, string? instanceName = null)
    {
        ArgumentNullException.ThrowIfNull(protocol);

        _deviceInfo = new DiscoveryResponse
        {
            InstanceName = instanceName ?? $"VirtualLCD:{devicePort} ({protocol.DisplayName})",
            DevicePort = devicePort,
            ProtocolId = protocol.ProtocolId,
            Width = protocol.Width,
            Height = protocol.Height,
            VendorId = protocol.VendorId,
            ProductId = protocol.ProductId,
            HidReportSize = protocol.HidReportSize
        };

        // Bind to discovery port
        _listener = new UdpClient();
        _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.Client.Bind(new IPEndPoint(IPAddress.Any, VirtualDeviceDiscovery.DiscoveryPort));
    }

    /// <summary>
    /// Start listening for discovery requests.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_listenTask != null)
        {
            return;
        }

        _listenTask = ListenAsync(_cts.Token);
    }

    /// <summary>
    /// Stop listening for discovery requests.
    /// </summary>
    public async Task StopAsync()
    {
        if (_listenTask == null)
        {
            return;
        }

        await _cts.CancelAsync();

        try
        {
            await _listenTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _listenTask = null;
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        var responseData = _deviceInfo.ToBytes();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await _listener.ReceiveAsync(cancellationToken);
                var request = DiscoveryRequest.FromBytes(result.Buffer);

                if (request?.IsValid() == true)
                {
                    // Respond to the requester
                    await _listener.SendAsync(responseData, result.RemoteEndPoint, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                // Socket error, continue listening
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Discovery responder error: {ex.Message}");
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cts.Cancel();
        _listener.Dispose();
        _cts.Dispose();
        _disposed = true;
    }
}
