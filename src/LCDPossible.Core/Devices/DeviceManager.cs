using LCDPossible.Core.Usb;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Core.Devices;

/// <summary>
/// Manages LCD device discovery, registration, and lifecycle.
/// </summary>
public sealed class DeviceManager : IDisposable
{
    private readonly IDeviceEnumerator _enumerator;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger<DeviceManager>? _logger;
    private readonly Dictionary<string, ILcdDevice> _activeDevices = [];
    private readonly Dictionary<(ushort Vid, ushort Pid), Func<HidDeviceInfo, ILcdDevice>> _driverFactories = [];
    private bool _disposed;

    public DeviceManager(IDeviceEnumerator enumerator, ILoggerFactory? loggerFactory = null)
    {
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<DeviceManager>();

        _enumerator.DeviceArrived += OnDeviceArrived;
        _enumerator.DeviceRemoved += OnDeviceRemoved;
    }

    /// <summary>
    /// Event raised when a supported device is discovered.
    /// </summary>
    public event EventHandler<LcdDeviceEventArgs>? DeviceDiscovered;

    /// <summary>
    /// Event raised when a device is disconnected.
    /// </summary>
    public event EventHandler<LcdDeviceEventArgs>? DeviceDisconnected;

    /// <summary>
    /// Gets all currently connected devices.
    /// </summary>
    public IReadOnlyCollection<ILcdDevice> ActiveDevices => _activeDevices.Values.ToList().AsReadOnly();

    /// <summary>
    /// Registers a driver factory for a specific VID/PID combination.
    /// </summary>
    /// <param name="vendorId">USB vendor ID.</param>
    /// <param name="productId">USB product ID.</param>
    /// <param name="factory">Factory function to create the driver.</param>
    public void RegisterDriver(ushort vendorId, ushort productId, Func<HidDeviceInfo, ILcdDevice> factory)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(factory);

        _driverFactories[(vendorId, productId)] = factory;
        _logger?.LogInformation(
            "Registered driver for VID:0x{VendorId:X4} PID:0x{ProductId:X4}",
            vendorId, productId);
    }

    /// <summary>
    /// Discovers all supported LCD devices.
    /// </summary>
    /// <returns>Collection of discovered devices.</returns>
    public IEnumerable<ILcdDevice> DiscoverDevices()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var discoveredDevices = new List<ILcdDevice>();

        foreach (var (vidPid, factory) in _driverFactories)
        {
            var hidDevices = _enumerator.EnumerateDevices(vidPid.Vid, vidPid.Pid);

            foreach (var hidDevice in hidDevices)
            {
                if (_activeDevices.ContainsKey(hidDevice.DevicePath))
                {
                    // Device already tracked
                    discoveredDevices.Add(_activeDevices[hidDevice.DevicePath]);
                    continue;
                }

                try
                {
                    var lcdDevice = factory(hidDevice);
                    _activeDevices[hidDevice.DevicePath] = lcdDevice;
                    lcdDevice.Disconnected += OnLcdDeviceDisconnected;
                    discoveredDevices.Add(lcdDevice);

                    _logger?.LogInformation("Discovered device: {Device}", lcdDevice.Info);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to create driver for device: {DevicePath}", hidDevice.DevicePath);
                }
            }
        }

        return discoveredDevices;
    }

    /// <summary>
    /// Gets a device by its unique identifier.
    /// </summary>
    /// <param name="uniqueId">The device's unique ID.</param>
    /// <returns>The device if found, null otherwise.</returns>
    public ILcdDevice? GetDevice(string uniqueId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _activeDevices.Values.FirstOrDefault(d => d.Info.UniqueId == uniqueId);
    }

    /// <summary>
    /// Starts monitoring for device changes.
    /// </summary>
    public void StartMonitoring()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _enumerator.StartMonitoring();
        _logger?.LogInformation("Started device monitoring");
    }

    /// <summary>
    /// Stops monitoring for device changes.
    /// </summary>
    public void StopMonitoring()
    {
        _enumerator.StopMonitoring();
        _logger?.LogInformation("Stopped device monitoring");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _enumerator.DeviceArrived -= OnDeviceArrived;
        _enumerator.DeviceRemoved -= OnDeviceRemoved;

        foreach (var device in _activeDevices.Values)
        {
            device.Disconnected -= OnLcdDeviceDisconnected;
            device.Dispose();
        }
        _activeDevices.Clear();

        _disposed = true;
    }

    private void OnDeviceArrived(object? sender, DeviceEventArgs e)
    {
        if (_driverFactories.TryGetValue((e.Device.VendorId, e.Device.ProductId), out var factory))
        {
            try
            {
                var lcdDevice = factory(e.Device);
                _activeDevices[e.Device.DevicePath] = lcdDevice;
                lcdDevice.Disconnected += OnLcdDeviceDisconnected;

                _logger?.LogInformation("Device connected: {Device}", lcdDevice.Info);
                DeviceDiscovered?.Invoke(this, new LcdDeviceEventArgs { Device = lcdDevice });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create driver for arrived device: {DevicePath}", e.Device.DevicePath);
            }
        }
    }

    private void OnDeviceRemoved(object? sender, DeviceEventArgs e)
    {
        if (_activeDevices.TryGetValue(e.Device.DevicePath, out var device))
        {
            _activeDevices.Remove(e.Device.DevicePath);
            device.Disconnected -= OnLcdDeviceDisconnected;

            _logger?.LogInformation("Device disconnected: {DevicePath}", e.Device.DevicePath);
            DeviceDisconnected?.Invoke(this, new LcdDeviceEventArgs { Device = device });

            device.Dispose();
        }
    }

    private void OnLcdDeviceDisconnected(object? sender, EventArgs e)
    {
        if (sender is ILcdDevice device)
        {
            var entry = _activeDevices.FirstOrDefault(kvp => kvp.Value == device);
            if (entry.Key != null)
            {
                _activeDevices.Remove(entry.Key);
                device.Disconnected -= OnLcdDeviceDisconnected;

                _logger?.LogInformation("Device unexpectedly disconnected: {Device}", device.Info);
                DeviceDisconnected?.Invoke(this, new LcdDeviceEventArgs { Device = device });
            }
        }
    }
}

/// <summary>
/// Event arguments for LCD device events.
/// </summary>
public sealed class LcdDeviceEventArgs : EventArgs
{
    public required ILcdDevice Device { get; init; }
}
