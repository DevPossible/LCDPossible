using System.Net;
using System.Net.Sockets;

namespace LCDPossible.Core.Usb;

/// <summary>
/// IHidDevice implementation that sends HID reports over UDP to a VirtualLCD instance.
/// </summary>
public sealed class VirtualHidDevice : IHidDevice
{
    private readonly IPEndPoint _endpoint;
    private UdpClient? _client;
    private bool _isOpen;
    private bool _disposed;

    /// <summary>
    /// Creates a new virtual HID device.
    /// </summary>
    /// <param name="host">Host address of the VirtualLCD instance.</param>
    /// <param name="port">UDP port of the VirtualLCD instance.</param>
    /// <param name="vendorId">USB Vendor ID to emulate.</param>
    /// <param name="productId">USB Product ID to emulate.</param>
    /// <param name="maxOutputReportLength">Maximum HID report size.</param>
    public VirtualHidDevice(
        string host,
        int port,
        ushort vendorId,
        ushort productId,
        int maxOutputReportLength = 513)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("Host cannot be empty", nameof(host));

        if (port < 1 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535");

        var address = host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            ? IPAddress.Loopback
            : IPAddress.Parse(host);

        _endpoint = new IPEndPoint(address, port);
        VendorId = vendorId;
        ProductId = productId;
        MaxOutputReportLength = maxOutputReportLength;
        DevicePath = $"virtual://{host}:{port}";
    }

    /// <summary>
    /// Creates a new virtual HID device from an endpoint.
    /// </summary>
    public VirtualHidDevice(
        IPEndPoint endpoint,
        ushort vendorId,
        ushort productId,
        int maxOutputReportLength = 513)
    {
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        VendorId = vendorId;
        ProductId = productId;
        MaxOutputReportLength = maxOutputReportLength;
        DevicePath = $"virtual://{endpoint.Address}:{endpoint.Port}";
    }

    /// <inheritdoc />
    public string DevicePath { get; }

    /// <inheritdoc />
    public ushort VendorId { get; }

    /// <inheritdoc />
    public ushort ProductId { get; }

    /// <inheritdoc />
    public string? Manufacturer => "Virtual";

    /// <inheritdoc />
    public string? ProductName => "Virtual LCD Device";

    /// <inheritdoc />
    public bool IsOpen => _isOpen && !_disposed;

    /// <inheritdoc />
    public int MaxOutputReportLength { get; }

    /// <inheritdoc />
    public int MaxInputReportLength => 0; // No input from virtual device

    /// <inheritdoc />
    public void Open()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isOpen)
        {
            throw new InvalidOperationException("Device is already open");
        }

        _client = new UdpClient();
        _client.Connect(_endpoint);
        _isOpen = true;
    }

    /// <inheritdoc />
    public void Close()
    {
        if (!_isOpen)
        {
            return;
        }

        _client?.Close();
        _client?.Dispose();
        _client = null;
        _isOpen = false;
    }

    /// <inheritdoc />
    public void Write(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_isOpen || _client == null)
        {
            throw new InvalidOperationException("Device is not open");
        }

        // Send as UDP datagram
        _client.Send(data);
    }

    /// <inheritdoc />
    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_isOpen || _client == null)
        {
            throw new InvalidOperationException("Device is not open");
        }

        // Send as UDP datagram
        await _client.SendAsync(data, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public int Read(Span<byte> buffer, int timeout = 1000)
    {
        // Virtual device doesn't support reading
        return 0;
    }

    /// <inheritdoc />
    public Task<int> ReadAsync(Memory<byte> buffer, int timeout = 1000, CancellationToken cancellationToken = default)
    {
        // Virtual device doesn't support reading
        return Task.FromResult(0);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Close();
        _disposed = true;
    }
}
