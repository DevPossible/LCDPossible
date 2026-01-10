using HidSharp;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Core.Usb;

/// <summary>
/// HidSharp-based implementation of IDeviceEnumerator.
/// </summary>
public sealed class HidSharpEnumerator : IDeviceEnumerator
{
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger<HidSharpEnumerator>? _logger;
    private readonly HashSet<string> _knownDevices = [];
    private Timer? _pollingTimer;
    private bool _isMonitoring;
    private bool _disposed;

    public HidSharpEnumerator(ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<HidSharpEnumerator>();
    }

    public event EventHandler<DeviceEventArgs>? DeviceArrived;
    public event EventHandler<DeviceEventArgs>? DeviceRemoved;

    public IEnumerable<HidDeviceInfo> EnumerateDevices()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return DeviceList.Local.GetHidDevices().Select(ToDeviceInfo);
    }

    public IEnumerable<HidDeviceInfo> EnumerateDevices(ushort vendorId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return DeviceList.Local.GetHidDevices(vendorId).Select(ToDeviceInfo);
    }

    public IEnumerable<HidDeviceInfo> EnumerateDevices(ushort vendorId, ushort productId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return DeviceList.Local.GetHidDevices(vendorId, productId).Select(ToDeviceInfo);
    }

    public IHidDevice OpenDevice(HidDeviceInfo deviceInfo)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(deviceInfo);

        var hidDevice = DeviceList.Local.GetHidDevices()
            .FirstOrDefault(d => d.DevicePath == deviceInfo.DevicePath)
            ?? throw new IOException($"Device not found: {deviceInfo.DevicePath}");

        var device = new HidSharpDevice(
            hidDevice,
            _loggerFactory?.CreateLogger<HidSharpDevice>());

        device.Open();
        return device;
    }

    public void StartMonitoring()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isMonitoring)
        {
            return;
        }

        // Initialize known devices
        _knownDevices.Clear();
        foreach (var device in EnumerateDevices())
        {
            _knownDevices.Add(device.DevicePath);
        }

        // HidSharp doesn't provide native device change notifications on all platforms,
        // so we use polling as a fallback
        _pollingTimer = new Timer(PollForDeviceChanges, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        _isMonitoring = true;

        _logger?.LogDebug("Started device monitoring with {Count} known devices", _knownDevices.Count);
    }

    public void StopMonitoring()
    {
        if (!_isMonitoring)
        {
            return;
        }

        _pollingTimer?.Dispose();
        _pollingTimer = null;
        _isMonitoring = false;

        _logger?.LogDebug("Stopped device monitoring");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopMonitoring();
        _disposed = true;
    }

    private void PollForDeviceChanges(object? state)
    {
        try
        {
            var currentDevices = DeviceList.Local.GetHidDevices()
                .Select(d => d.DevicePath)
                .ToHashSet();

            // Check for new devices
            var arrivedDevices = currentDevices.Except(_knownDevices).ToList();
            foreach (var devicePath in arrivedDevices)
            {
                var hidDevice = DeviceList.Local.GetHidDevices()
                    .FirstOrDefault(d => d.DevicePath == devicePath);

                if (hidDevice != null)
                {
                    var deviceInfo = ToDeviceInfo(hidDevice);
                    _logger?.LogInformation("Device arrived: {Device}", deviceInfo);
                    DeviceArrived?.Invoke(this, new DeviceEventArgs { Device = deviceInfo });
                }
            }

            // Check for removed devices
            var removedDevices = _knownDevices.Except(currentDevices).ToList();
            foreach (var devicePath in removedDevices)
            {
                // Create a minimal device info for the removed device
                var deviceInfo = new HidDeviceInfo
                {
                    DevicePath = devicePath,
                    VendorId = 0,
                    ProductId = 0,
                };
                _logger?.LogInformation("Device removed: {DevicePath}", devicePath);
                DeviceRemoved?.Invoke(this, new DeviceEventArgs { Device = deviceInfo });
            }

            // Update known devices
            _knownDevices.Clear();
            foreach (var path in currentDevices)
            {
                _knownDevices.Add(path);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error polling for device changes");
        }
    }

    private static HidDeviceInfo ToDeviceInfo(HidDevice device)
    {
        return new HidDeviceInfo
        {
            DevicePath = device.DevicePath,
            VendorId = (ushort)device.VendorID,
            ProductId = (ushort)device.ProductID,
            Manufacturer = TryGetProperty(() => device.GetManufacturer()),
            ProductName = TryGetProperty(() => device.GetProductName()),
            SerialNumber = TryGetProperty(() => device.GetSerialNumber()),
            MaxInputReportLength = device.GetMaxInputReportLength(),
            MaxOutputReportLength = device.GetMaxOutputReportLength(),
            MaxFeatureReportLength = device.GetMaxFeatureReportLength(),
        };
    }

    private static string? TryGetProperty(Func<string> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }
}
