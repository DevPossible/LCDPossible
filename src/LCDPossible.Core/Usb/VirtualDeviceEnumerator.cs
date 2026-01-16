using System.Net;

namespace LCDPossible.Core.Usb;

/// <summary>
/// Device enumerator that provides virtual LCD devices connecting via UDP.
/// </summary>
public sealed class VirtualDeviceEnumerator : IDeviceEnumerator
{
    private readonly List<VirtualDeviceConfig> _devices = [];
    private bool _disposed;

    /// <summary>
    /// Add a virtual device configuration.
    /// </summary>
    /// <param name="config">Device configuration.</param>
    public void AddDevice(VirtualDeviceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _devices.Add(config);
    }

    /// <summary>
    /// Clear all configured virtual devices.
    /// </summary>
    public void ClearDevices()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _devices.Clear();
    }

    /// <summary>
    /// Add a virtual device with the specified parameters.
    /// </summary>
    public void AddDevice(
        string host,
        int port,
        ushort vendorId,
        ushort productId,
        string? name = null,
        int maxOutputReportLength = 513)
    {
        _devices.Add(new VirtualDeviceConfig
        {
            Host = host,
            Port = port,
            VendorId = vendorId,
            ProductId = productId,
            Name = name ?? $"Virtual LCD ({host}:{port})",
            MaxOutputReportLength = maxOutputReportLength
        });
    }

    /// <inheritdoc />
    public IEnumerable<HidDeviceInfo> EnumerateDevices()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        foreach (var config in _devices)
        {
            yield return CreateHidDeviceInfo(config);
        }
    }

    /// <inheritdoc />
    public IEnumerable<HidDeviceInfo> EnumerateDevices(ushort vendorId)
    {
        return EnumerateDevices().Where(d => d.VendorId == vendorId);
    }

    /// <inheritdoc />
    public IEnumerable<HidDeviceInfo> EnumerateDevices(ushort vendorId, ushort productId)
    {
        return EnumerateDevices().Where(d => d.VendorId == vendorId && d.ProductId == productId);
    }

    /// <inheritdoc />
    public IHidDevice OpenDevice(HidDeviceInfo deviceInfo)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(deviceInfo);

        // Find matching config
        var config = _devices.FirstOrDefault(d =>
            d.VendorId == deviceInfo.VendorId &&
            d.ProductId == deviceInfo.ProductId &&
            deviceInfo.DevicePath.Contains($"{d.Host}:{d.Port}"));

        if (config == null)
        {
            throw new InvalidOperationException($"No virtual device configuration found for {deviceInfo}");
        }

        var device = new VirtualHidDevice(
            config.Host,
            config.Port,
            config.VendorId,
            config.ProductId,
            config.MaxOutputReportLength);

        device.Open();
        return device;
    }

    /// <inheritdoc />
    public void StartMonitoring()
    {
        // Virtual devices don't support hot-plug monitoring
        // They are configured at startup
    }

    /// <inheritdoc />
    public void StopMonitoring()
    {
        // Nothing to stop
    }

    /// <inheritdoc />
    public event EventHandler<DeviceEventArgs>? DeviceArrived;

    /// <inheritdoc />
    public event EventHandler<DeviceEventArgs>? DeviceRemoved;

    private static HidDeviceInfo CreateHidDeviceInfo(VirtualDeviceConfig config)
    {
        return new HidDeviceInfo
        {
            DevicePath = $"virtual://{config.Host}:{config.Port}",
            VendorId = config.VendorId,
            ProductId = config.ProductId,
            Manufacturer = "Virtual",
            ProductName = config.Name,
            SerialNumber = $"VIRTUAL-{config.Port}",
            MaxInputReportLength = 0,
            MaxOutputReportLength = config.MaxOutputReportLength,
            MaxFeatureReportLength = 0
        };
    }

    /// <summary>
    /// Simulates a device arrival event for testing.
    /// </summary>
    public void SimulateDeviceArrival(VirtualDeviceConfig config)
    {
        var deviceInfo = CreateHidDeviceInfo(config);
        DeviceArrived?.Invoke(this, new DeviceEventArgs { Device = deviceInfo });
    }

    /// <summary>
    /// Simulates a device removal event for testing.
    /// </summary>
    public void SimulateDeviceRemoval(VirtualDeviceConfig config)
    {
        var deviceInfo = CreateHidDeviceInfo(config);
        DeviceRemoved?.Invoke(this, new DeviceEventArgs { Device = deviceInfo });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _devices.Clear();
        _disposed = true;
    }
}

/// <summary>
/// Configuration for a virtual LCD device.
/// </summary>
public sealed class VirtualDeviceConfig
{
    /// <summary>
    /// Host address of the VirtualLCD instance.
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// UDP port of the VirtualLCD instance.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// USB Vendor ID to emulate.
    /// </summary>
    public required ushort VendorId { get; init; }

    /// <summary>
    /// USB Product ID to emulate.
    /// </summary>
    public required ushort ProductId { get; init; }

    /// <summary>
    /// Display name for the virtual device.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Maximum HID report size.
    /// </summary>
    public int MaxOutputReportLength { get; init; } = 513;

    /// <summary>
    /// Parse a virtual device endpoint string.
    /// Format: udp://host:port or host:port
    /// </summary>
    public static VirtualDeviceConfig Parse(string endpoint, ushort vendorId, ushort productId)
    {
        var uri = endpoint.StartsWith("udp://", StringComparison.OrdinalIgnoreCase)
            ? endpoint
            : $"udp://{endpoint}";

        var parsed = new Uri(uri);

        return new VirtualDeviceConfig
        {
            Host = parsed.Host,
            Port = parsed.Port > 0 ? parsed.Port : 5302,
            VendorId = vendorId,
            ProductId = productId
        };
    }
}
