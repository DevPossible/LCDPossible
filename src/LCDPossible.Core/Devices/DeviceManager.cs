using LCDPossible.Core.Plugins;
using LCDPossible.Core.Usb;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Core.Devices;

/// <summary>
/// Manages LCD device discovery, registration, and lifecycle.
/// </summary>
public sealed class DeviceManager : IDisposable
{
    private readonly IDeviceEnumerator _enumerator;
    private readonly DevicePluginManager? _pluginManager;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger<DeviceManager>? _logger;
    private readonly Dictionary<string, ILcdDevice> _activeDevices = [];
    private readonly Dictionary<(ushort Vid, ushort Pid), Func<HidDeviceInfo, ILcdDevice>> _driverFactories = [];
    private bool _disposed;

    /// <summary>
    /// Creates a new DeviceManager with optional plugin support.
    /// </summary>
    /// <param name="enumerator">The device enumerator.</param>
    /// <param name="pluginManager">Optional device plugin manager for plugin-based drivers.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    public DeviceManager(
        IDeviceEnumerator enumerator,
        DevicePluginManager? pluginManager = null,
        ILoggerFactory? loggerFactory = null)
    {
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        _pluginManager = pluginManager;
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
    /// Gets the device plugin manager if configured.
    /// </summary>
    public DevicePluginManager? PluginManager => _pluginManager;

    /// <summary>
    /// Registers a driver factory for a specific VID/PID combination.
    /// This is for backward compatibility; prefer using DevicePluginManager.
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
        var processedPaths = new HashSet<string>();

        // First, discover devices via plugin manager if available
        if (_pluginManager is not null)
        {
            foreach (var supported in _pluginManager.GetSupportedDevices())
            {
                var hidDevices = _enumerator.EnumerateDevices(supported.VendorId, supported.ProductId);

                foreach (var hidDevice in hidDevices)
                {
                    if (processedPaths.Contains(hidDevice.DevicePath))
                        continue;

                    processedPaths.Add(hidDevice.DevicePath);

                    if (_activeDevices.TryGetValue(hidDevice.DevicePath, out var existingDevice))
                    {
                        discoveredDevices.Add(existingDevice);
                        continue;
                    }

                    try
                    {
                        var lcdDevice = _pluginManager.CreatePhysicalDevice(hidDevice, _enumerator);
                        if (lcdDevice is not null)
                        {
                            _activeDevices[hidDevice.DevicePath] = lcdDevice;
                            lcdDevice.Disconnected += OnLcdDeviceDisconnected;
                            discoveredDevices.Add(lcdDevice);

                            _logger?.LogInformation("Discovered device via plugin: {Device}", lcdDevice.Info);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to create plugin driver for device: {DevicePath}", hidDevice.DevicePath);
                    }
                }
            }
        }

        // Then, use registered driver factories (for backward compatibility)
        foreach (var (vidPid, factory) in _driverFactories)
        {
            var hidDevices = _enumerator.EnumerateDevices(vidPid.Vid, vidPid.Pid);

            foreach (var hidDevice in hidDevices)
            {
                if (processedPaths.Contains(hidDevice.DevicePath))
                    continue;

                processedPaths.Add(hidDevice.DevicePath);

                if (_activeDevices.TryGetValue(hidDevice.DevicePath, out var existingDevice))
                {
                    discoveredDevices.Add(existingDevice);
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
    /// Gets all supported devices from plugins and registered factories.
    /// </summary>
    public IEnumerable<SupportedDeviceInfo> GetAllSupportedDevices()
    {
        // Get from plugin manager
        if (_pluginManager is not null)
        {
            foreach (var device in _pluginManager.GetSupportedDevices())
            {
                yield return device;
            }
        }

        // Get from registered factories (backward compatibility)
        foreach (var (vidPid, _) in _driverFactories)
        {
            // Skip if already provided by plugin
            if (_pluginManager?.IsDeviceSupported(vidPid.Vid, vidPid.Pid) == true)
                continue;

            yield return new SupportedDeviceInfo
            {
                VendorId = vidPid.Vid,
                ProductId = vidPid.Pid,
                DeviceName = $"Device 0x{vidPid.Vid:X4}:0x{vidPid.Pid:X4}",
                ProtocolId = "legacy"
            };
        }
    }

    /// <summary>
    /// Checks if a device is supported.
    /// </summary>
    public bool IsDeviceSupported(ushort vendorId, ushort productId)
    {
        if (_pluginManager?.IsDeviceSupported(vendorId, productId) == true)
            return true;

        return _driverFactories.ContainsKey((vendorId, productId));
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
        ILcdDevice? lcdDevice = null;

        // Try plugin manager first
        if (_pluginManager?.IsDeviceSupported(e.Device.VendorId, e.Device.ProductId) == true)
        {
            try
            {
                lcdDevice = _pluginManager.CreatePhysicalDevice(e.Device, _enumerator);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create plugin driver for arrived device: {DevicePath}", e.Device.DevicePath);
            }
        }

        // Fall back to registered factory
        if (lcdDevice is null && _driverFactories.TryGetValue((e.Device.VendorId, e.Device.ProductId), out var factory))
        {
            try
            {
                lcdDevice = factory(e.Device);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create driver for arrived device: {DevicePath}", e.Device.DevicePath);
            }
        }

        if (lcdDevice is not null)
        {
            _activeDevices[e.Device.DevicePath] = lcdDevice;
            lcdDevice.Disconnected += OnLcdDeviceDisconnected;

            _logger?.LogInformation("Device connected: {Device}", lcdDevice.Info);
            DeviceDiscovered?.Invoke(this, new LcdDeviceEventArgs { Device = lcdDevice });
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
