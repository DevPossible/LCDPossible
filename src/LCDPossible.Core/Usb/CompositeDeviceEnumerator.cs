using LCDPossible.Core.Usb.Discovery;

namespace LCDPossible.Core.Usb;

/// <summary>
/// Device enumerator that combines multiple enumerators (real HID + virtual devices).
/// Discovers virtual devices first, then real HID devices.
/// </summary>
public sealed class CompositeDeviceEnumerator : IDeviceEnumerator
{
    private readonly IDeviceEnumerator _realEnumerator;
    private readonly VirtualDeviceEnumerator _virtualEnumerator;
    private readonly VirtualDeviceDiscoveryClient _discoveryClient;
    private readonly TimeSpan _discoveryTimeout;
    private bool _disposed;

    /// <summary>
    /// Creates a composite enumerator combining real and virtual devices.
    /// </summary>
    /// <param name="realEnumerator">Enumerator for real HID devices.</param>
    /// <param name="discoveryTimeout">Timeout for virtual device discovery.</param>
    public CompositeDeviceEnumerator(
        IDeviceEnumerator realEnumerator,
        TimeSpan? discoveryTimeout = null)
    {
        _realEnumerator = realEnumerator ?? throw new ArgumentNullException(nameof(realEnumerator));
        _virtualEnumerator = new VirtualDeviceEnumerator();
        _discoveryClient = new VirtualDeviceDiscoveryClient();
        _discoveryTimeout = discoveryTimeout ?? VirtualDeviceDiscovery.DefaultTimeout;
    }

    /// <summary>
    /// Refresh the list of discovered virtual devices.
    /// Call this before enumerating if you want to discover new virtual devices.
    /// </summary>
    public async Task RefreshVirtualDevicesAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var discovered = await _discoveryClient.DiscoverAsync(_discoveryTimeout, cancellationToken).ConfigureAwait(false);

        // Clear existing virtual devices and add discovered ones
        _virtualEnumerator.ClearDevices();

        foreach (var device in discovered)
        {
            _virtualEnumerator.AddDevice(device.ToConfig());
        }
    }

    /// <inheritdoc />
    public IEnumerable<HidDeviceInfo> EnumerateDevices()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Virtual devices first (sorted alphabetically by their instance names)
        foreach (var device in _virtualEnumerator.EnumerateDevices())
        {
            yield return device;
        }

        // Then real HID devices
        foreach (var device in _realEnumerator.EnumerateDevices())
        {
            yield return device;
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

        // Check if it's a virtual device (path starts with "virtual://")
        if (deviceInfo.DevicePath.StartsWith("virtual://", StringComparison.OrdinalIgnoreCase))
        {
            return _virtualEnumerator.OpenDevice(deviceInfo);
        }

        // Otherwise it's a real device
        return _realEnumerator.OpenDevice(deviceInfo);
    }

    /// <inheritdoc />
    public void StartMonitoring()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _realEnumerator.StartMonitoring();
        _virtualEnumerator.StartMonitoring();
    }

    /// <inheritdoc />
    public void StopMonitoring()
    {
        _realEnumerator.StopMonitoring();
        _virtualEnumerator.StopMonitoring();
    }

    /// <inheritdoc />
    public event EventHandler<DeviceEventArgs>? DeviceArrived
    {
        add
        {
            _realEnumerator.DeviceArrived += value;
            _virtualEnumerator.DeviceArrived += value;
        }
        remove
        {
            _realEnumerator.DeviceArrived -= value;
            _virtualEnumerator.DeviceArrived -= value;
        }
    }

    /// <inheritdoc />
    public event EventHandler<DeviceEventArgs>? DeviceRemoved
    {
        add
        {
            _realEnumerator.DeviceRemoved += value;
            _virtualEnumerator.DeviceRemoved += value;
        }
        remove
        {
            _realEnumerator.DeviceRemoved -= value;
            _virtualEnumerator.DeviceRemoved -= value;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _discoveryClient.Dispose();
        _virtualEnumerator.Dispose();
        // Don't dispose _realEnumerator - it was passed to us
        _disposed = true;
    }
}
