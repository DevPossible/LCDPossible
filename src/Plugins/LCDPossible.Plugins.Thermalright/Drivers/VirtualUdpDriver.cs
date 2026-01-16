using System.Net;
using System.Net.Sockets;
using LCDPossible.Core.Devices;
using LCDPossible.Plugins.Thermalright.Protocol;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Plugins.Thermalright.Drivers;

/// <summary>
/// Virtual device driver that sends protocol data over UDP.
/// Used for communicating with LCD simulators.
/// </summary>
public sealed class VirtualUdpDriver : ILcdDevice
{
    private readonly string _protocolId;
    private readonly IPEndPoint _endpoint;
    private readonly LcdCapabilities _capabilities;
    private readonly ILogger? _logger;
    private UdpClient? _udpClient;
    private bool _disposed;
    private bool _connected;

    public VirtualUdpDriver(
        string protocolId,
        string endpoint,
        LcdCapabilities capabilities,
        ILogger? logger = null)
    {
        _protocolId = protocolId ?? throw new ArgumentNullException(nameof(protocolId));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        _logger = logger;
        _endpoint = ParseEndpoint(endpoint);

        Info = new DeviceInfo(
            VendorId: 0xFFFF, // Virtual device
            ProductId: 0xFFFF,
            Name: $"Virtual {protocolId}",
            Manufacturer: "LCDPossible",
            DriverName: nameof(VirtualUdpDriver),
            DevicePath: $"udp://{_endpoint}",
            SerialNumber: null);
    }

    public DeviceInfo Info { get; }

    public LcdCapabilities Capabilities => _capabilities;

    public bool IsConnected => _connected && _udpClient != null;

    public event EventHandler? Disconnected;

    private void OnDisconnected()
    {
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsConnected)
        {
            return Task.CompletedTask;
        }

        try
        {
            _udpClient = new UdpClient();
            _udpClient.Connect(_endpoint);
            _connected = true;
            _logger?.LogInformation("Connected to virtual device at {Endpoint}", _endpoint);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to virtual device at {Endpoint}", _endpoint);
            throw;
        }

        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_udpClient != null)
        {
            _udpClient.Close();
            _udpClient.Dispose();
            _udpClient = null;
            _connected = false;
            _logger?.LogInformation("Disconnected from virtual device at {Endpoint}", _endpoint);
            OnDisconnected();
        }

        return Task.CompletedTask;
    }

    public async Task SendFrameAsync(ReadOnlyMemory<byte> frameData, ColorFormat format, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsConnected || _udpClient == null)
        {
            throw new InvalidOperationException("Device is not connected.");
        }

        if (!Capabilities.SupportsFormat(format))
        {
            throw new ArgumentException($"Unsupported color format: {format}", nameof(format));
        }

        var compressionType = format switch
        {
            ColorFormat.Jpeg => TrofeoVisionProtocol.CompressionJpeg,
            ColorFormat.Rgb565 => TrofeoVisionProtocol.CompressionRgb565,
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };

        // Build data packets (no HID report ID prefix)
        var packets = TrofeoVisionProtocol.BuildDataPackets(frameData.Span, compressionType);

        foreach (var packet in packets)
        {
            await _udpClient.SendAsync(packet, cancellationToken).ConfigureAwait(false);
        }

        _logger?.LogTrace("Sent frame: {ByteCount} bytes in {PacketCount} UDP packets", frameData.Length, packets.Count);
    }

    public Task SetBrightnessAsync(byte brightness, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsConnected)
        {
            throw new InvalidOperationException("Device is not connected.");
        }

        _logger?.LogDebug("Setting brightness to {Brightness} (virtual)", brightness);

        // Virtual devices may not support brightness
        return Task.CompletedTask;
    }

    public Task SetOrientationAsync(Orientation orientation, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsConnected)
        {
            throw new InvalidOperationException("Device is not connected.");
        }

        _logger?.LogDebug("Setting orientation to {Orientation} (virtual)", orientation);

        // Virtual devices may not support orientation
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _udpClient?.Dispose();
        _udpClient = null;
        _connected = false;
        _disposed = true;
    }

    private static IPEndPoint ParseEndpoint(string endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        var parts = endpoint.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
        {
            throw new ArgumentException($"Invalid endpoint format: {endpoint}. Expected host:port", nameof(endpoint));
        }

        if (IPAddress.TryParse(parts[0], out var ip))
        {
            return new IPEndPoint(ip, port);
        }

        // Resolve hostname
        var addresses = Dns.GetHostAddresses(parts[0]);
        if (addresses.Length == 0)
        {
            throw new ArgumentException($"Could not resolve host: {parts[0]}", nameof(endpoint));
        }

        return new IPEndPoint(addresses[0], port);
    }
}
