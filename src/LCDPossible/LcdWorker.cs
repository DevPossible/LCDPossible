using LCDPossible.Core.Configuration;
using LCDPossible.Core.Devices;
using LCDPossible.Core.Monitoring;
using LCDPossible.Core.Plugins;
using LCDPossible.Core.Rendering;
using LCDPossible.Monitoring;
using LCDPossible.Panels;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible;

/// <summary>
/// Background worker that manages LCD devices and rendering.
/// </summary>
public sealed class LcdWorker : BackgroundService
{
    private readonly DeviceManager _deviceManager;
    private readonly ProfileLoader _profileLoader;
    private readonly PluginManager _pluginManager;
    private readonly ILogger<LcdWorker> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly LcdPossibleOptions _options;
    private readonly JpegImageEncoder _encoder;
    private readonly Dictionary<string, ILcdDevice> _connectedDevices = [];
    private readonly Dictionary<string, Image<Rgba32>?> _staticImages = [];
    private readonly Dictionary<string, SlideshowManager> _slideshows = [];
    private readonly Dictionary<string, IDisplayPanel> _singlePanels = [];

    private ISystemInfoProvider? _systemProvider;
    private IProxmoxProvider? _proxmoxProvider;
    private PanelFactory? _panelFactory;
    private DisplayProfile? _displayProfile;

    public LcdWorker(
        DeviceManager deviceManager,
        ProfileLoader profileLoader,
        PluginManager pluginManager,
        IOptions<LcdPossibleOptions> options,
        ILogger<LcdWorker> logger,
        ILoggerFactory loggerFactory)
    {
        _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
        _profileLoader = profileLoader ?? throw new ArgumentNullException(nameof(profileLoader));
        _pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _encoder = new JpegImageEncoder { Quality = _options.General.JpegQuality };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LcdWorker starting...");

        // Load display profile from system location
        _displayProfile = _profileLoader.LoadProfile();
        _logger.LogInformation("Using display profile: {ProfileName}", _displayProfile.Name);

        // Subscribe to device events
        _deviceManager.DeviceDiscovered += OnDeviceDiscovered;
        _deviceManager.DeviceDisconnected += OnDeviceDisconnected;

        try
        {
            // Initialize monitoring providers
            await InitializeProvidersAsync(stoppingToken);

            // Initial device discovery
            await DiscoverAndConnectDevicesAsync(stoppingToken);

            // Start device monitoring for hot-plug
            _deviceManager.StartMonitoring();

            // Initialize slideshows and panels for each device
            await InitializeDisplayModesAsync(stoppingToken);

            // Main render loop
            var frameDelay = TimeSpan.FromMilliseconds(1000.0 / _options.General.TargetFrameRate);

            while (!stoppingToken.IsCancellationRequested)
            {
                var frameStart = DateTime.UtcNow;

                try
                {
                    await RenderFrameToAllDevicesAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error during frame render");
                }

                // Calculate remaining time to maintain target frame rate
                var elapsed = DateTime.UtcNow - frameStart;
                var remaining = frameDelay - elapsed;

                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining, stoppingToken);
                }
            }
        }
        finally
        {
            _deviceManager.StopMonitoring();
            _deviceManager.DeviceDiscovered -= OnDeviceDiscovered;
            _deviceManager.DeviceDisconnected -= OnDeviceDisconnected;

            // Cleanup
            foreach (var slideshow in _slideshows.Values)
            {
                slideshow.Dispose();
            }
            _slideshows.Clear();

            foreach (var panel in _singlePanels.Values)
            {
                panel.Dispose();
            }
            _singlePanels.Clear();

            foreach (var image in _staticImages.Values)
            {
                image?.Dispose();
            }
            _staticImages.Clear();

            _systemProvider?.Dispose();
            _proxmoxProvider?.Dispose();

            // Disconnect all devices
            foreach (var device in _connectedDevices.Values)
            {
                await device.DisconnectAsync();
            }
            _connectedDevices.Clear();

            _logger.LogInformation("LcdWorker stopped.");
        }
    }

    private async Task InitializeProvidersAsync(CancellationToken cancellationToken)
    {
        // Use stub system provider - actual hardware monitoring is done by the Core plugin
        _systemProvider = new StubSystemInfoProvider();
        await _systemProvider.InitializeAsync(cancellationToken);
        _logger.LogInformation("System info provider initialized (stub - plugins provide real data)");

        // Initialize Proxmox if configured
        if (_options.Proxmox.Enabled)
        {
            try
            {
                _proxmoxProvider = new ProxmoxProvider(
                    Options.Create(_options),
                    _loggerFactory.CreateLogger<ProxmoxProvider>());
                await _proxmoxProvider.InitializeAsync(cancellationToken);
                _logger.LogInformation("Proxmox provider initialized");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Proxmox provider");
            }
        }

        // Discover available plugins
        _pluginManager.DiscoverPlugins();
        _logger.LogInformation("Discovered {Count} plugins", _pluginManager.DiscoveredPlugins.Count);

        // Create panel factory with color scheme from profile
        _panelFactory = new PanelFactory(
            _pluginManager,
            _systemProvider,
            _proxmoxProvider,
            _loggerFactory);

        if (_displayProfile != null)
        {
            _panelFactory.SetColorScheme(_displayProfile.Colors);
        }
    }

    private async Task InitializeDisplayModesAsync(CancellationToken cancellationToken)
    {
        // Always initialize a default slideshow from the display profile
        // This is used for any devices that don't have specific configuration
        await InitializeDefaultSlideshowAsync(cancellationToken);

        // Initialize specific device configurations
        foreach (var (deviceId, deviceOptions) in _options.Devices)
        {
            var mode = deviceOptions.Mode?.ToLowerInvariant() ?? "slideshow";

            switch (mode)
            {
                case "static":
                    LoadStaticImage(deviceId, deviceOptions.ImagePath);
                    break;

                case "panel" when !string.IsNullOrEmpty(deviceOptions.Panel):
                    await InitializeSinglePanelAsync(deviceId, deviceOptions.Panel, cancellationToken);
                    break;

                case "slideshow":
                    await InitializeSlideshowAsync(deviceId, deviceOptions, cancellationToken);
                    break;
            }
        }
    }

    private async Task InitializeDefaultSlideshowAsync(CancellationToken cancellationToken)
    {
        if (_panelFactory == null || _displayProfile == null)
        {
            return;
        }

        var items = _displayProfile.ToSlideshowItems();
        if (items.Count == 0)
        {
            _logger.LogWarning("Default display profile has no slides");
            return;
        }

        var slideshow = new SlideshowManager(
            _panelFactory,
            items,
            _loggerFactory.CreateLogger<SlideshowManager>());

        await slideshow.InitializeAsync(cancellationToken);
        _slideshows["default"] = slideshow;
        _logger.LogInformation("Initialized default slideshow with {Count} panels from profile '{ProfileName}'",
            items.Count, _displayProfile.Name);
    }

    private void LoadStaticImage(string deviceId, string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            return;
        }

        try
        {
            _staticImages[deviceId] = Image.Load<Rgba32>(imagePath);
            _logger.LogInformation("Loaded static image for {DeviceId}: {Path}", deviceId, imagePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load static image for {DeviceId}: {Path}", deviceId, imagePath);
        }
    }

    private async Task InitializeSinglePanelAsync(string deviceId, string panelType, CancellationToken cancellationToken)
    {
        if (_panelFactory == null)
        {
            return;
        }

        var panel = _panelFactory.CreatePanel(panelType);
        if (panel != null)
        {
            await panel.InitializeAsync(cancellationToken);
            _singlePanels[deviceId] = panel;
            _logger.LogInformation("Initialized panel '{PanelType}' for device {DeviceId}", panelType, deviceId);
        }
    }

    private async Task InitializeSlideshowAsync(string deviceId, DeviceOptions deviceOptions, CancellationToken cancellationToken)
    {
        if (_panelFactory == null)
        {
            return;
        }

        // Try to get items from device-specific config first
        var items = deviceOptions.GetSlideshowItems();

        // Fall back to display profile if no items configured
        if (items.Count == 0 && _displayProfile != null)
        {
            _logger.LogInformation("Using display profile '{ProfileName}' for device {DeviceId}",
                _displayProfile.Name, deviceId);
            items = _displayProfile.ToSlideshowItems();
        }

        if (items.Count == 0)
        {
            _logger.LogWarning("No valid slideshow items for device {DeviceId}", deviceId);
            return;
        }

        var slideshow = new SlideshowManager(
            _panelFactory,
            items,
            _loggerFactory.CreateLogger<SlideshowManager>());

        await slideshow.InitializeAsync(cancellationToken);
        _slideshows[deviceId] = slideshow;
        _logger.LogInformation("Initialized slideshow with {Count} items for device {DeviceId}", items.Count, deviceId);
    }

    private async Task DiscoverAndConnectDevicesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Discovering LCD devices...");

        var devices = _deviceManager.DiscoverDevices().ToList();

        if (devices.Count == 0)
        {
            _logger.LogWarning("No supported LCD devices found.");
            _logger.LogInformation("Supported devices:");
            foreach (var supported in DriverRegistry.SupportedDevices)
            {
                _logger.LogInformation("  - {DeviceName} (VID:0x{Vid:X4} PID:0x{Pid:X4})",
                    supported.DeviceName, supported.VendorId, supported.ProductId);
            }
            return;
        }

        _logger.LogInformation("Found {Count} device(s):", devices.Count);

        foreach (var device in devices)
        {
            _logger.LogInformation("  - {Device}", device.Info);

            try
            {
                await device.ConnectAsync(cancellationToken);
                _connectedDevices[device.Info.UniqueId] = device;
                _logger.LogInformation("Connected to {DeviceName}", device.Info.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to {DeviceName}", device.Info.Name);
            }
        }
    }

    private DeviceOptions GetDeviceOptions(ILcdDevice device)
    {
        // Try to find device-specific config, fall back to "default"
        if (_options.Devices.TryGetValue(device.Info.UniqueId, out var options))
        {
            return options;
        }
        if (_options.Devices.TryGetValue("default", out var defaultOptions))
        {
            return defaultOptions;
        }
        return new DeviceOptions();
    }

    private string GetDeviceConfigKey(ILcdDevice device)
    {
        // Return the config key for this device (device-specific or "default")
        if (_options.Devices.ContainsKey(device.Info.UniqueId))
        {
            return device.Info.UniqueId;
        }
        return "default";
    }

    private async Task RenderFrameToAllDevicesAsync(CancellationToken cancellationToken)
    {
        if (_connectedDevices.Count == 0)
        {
            return;
        }

        foreach (var device in _connectedDevices.Values)
        {
            if (!device.IsConnected)
            {
                continue;
            }

            try
            {
                var deviceOptions = GetDeviceOptions(device);
                var configKey = GetDeviceConfigKey(device);
                var mode = deviceOptions.Mode?.ToLowerInvariant() ?? "animation";

                Image<Rgba32>? frame = null;
                var disposeFrame = true;

                switch (mode)
                {
                    case "off":
                        continue; // Skip this device

                    case "static":
                        frame = GetStaticFrame(configKey, device.Capabilities);
                        disposeFrame = frame != _staticImages.GetValueOrDefault(configKey);
                        break;

                    case "panel":
                        frame = await GetPanelFrameAsync(configKey, device.Capabilities, cancellationToken);
                        break;

                    case "slideshow":
                        frame = await GetSlideshowFrameAsync(configKey, device.Capabilities, cancellationToken);
                        break;

                    case "clock":
                        frame = GenerateClockDisplay(device.Capabilities);
                        break;

                    case "animation":
                    default:
                        frame = GenerateAnimatedPattern(device.Capabilities);
                        break;
                }

                if (frame != null)
                {
                    // Resize if needed
                    if (frame.Width != device.Capabilities.Width || frame.Height != device.Capabilities.Height)
                    {
                        var resized = frame.Clone(ctx => ctx.Resize(device.Capabilities.Width, device.Capabilities.Height));
                        if (disposeFrame) frame.Dispose();
                        frame = resized;
                        disposeFrame = true;
                    }

                    var encoded = _encoder.Encode(frame, device.Capabilities);
                    await device.SendFrameAsync(encoded, ColorFormat.Jpeg, cancellationToken);

                    if (disposeFrame) frame.Dispose();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to send frame to {Device}", device.Info.Name);
            }
        }
    }

    private Image<Rgba32>? GetStaticFrame(string configKey, LcdCapabilities capabilities)
    {
        if (_staticImages.TryGetValue(configKey, out var image) && image != null)
        {
            return image;
        }
        if (_staticImages.TryGetValue("default", out image) && image != null)
        {
            return image;
        }
        return GenerateSolidColor(capabilities, new Rgba32(0, 0, 0));
    }

    private async Task<Image<Rgba32>?> GetPanelFrameAsync(string configKey, LcdCapabilities capabilities, CancellationToken cancellationToken)
    {
        if (_singlePanels.TryGetValue(configKey, out var panel))
        {
            return await panel.RenderFrameAsync(capabilities.Width, capabilities.Height, cancellationToken);
        }
        if (_singlePanels.TryGetValue("default", out panel))
        {
            return await panel.RenderFrameAsync(capabilities.Width, capabilities.Height, cancellationToken);
        }
        return GenerateSolidColor(capabilities, new Rgba32(30, 30, 40));
    }

    private async Task<Image<Rgba32>?> GetSlideshowFrameAsync(string configKey, LcdCapabilities capabilities, CancellationToken cancellationToken)
    {
        if (_slideshows.TryGetValue(configKey, out var slideshow))
        {
            return await slideshow.RenderCurrentFrameAsync(capabilities.Width, capabilities.Height, cancellationToken);
        }
        if (_slideshows.TryGetValue("default", out slideshow))
        {
            return await slideshow.RenderCurrentFrameAsync(capabilities.Width, capabilities.Height, cancellationToken);
        }
        return GenerateSolidColor(capabilities, new Rgba32(30, 30, 40));
    }

    private static Image<Rgba32> GenerateSolidColor(LcdCapabilities capabilities, Rgba32 color)
    {
        var image = new Image<Rgba32>(capabilities.Width, capabilities.Height);
        image.Mutate(ctx => ctx.BackgroundColor(new Color(color)));
        return image;
    }

    private static Image<Rgba32> GenerateClockDisplay(LcdCapabilities capabilities)
    {
        var image = new Image<Rgba32>(capabilities.Width, capabilities.Height);
        var now = DateTime.Now;

        // Simple clock - gradient background with time indication through color
        var hue = (float)(now.Hour * 15 + now.Minute / 4); // 0-360 based on time

        image.Mutate(ctx =>
        {
            for (var y = 0; y < capabilities.Height; y++)
            {
                var t = (float)y / capabilities.Height;
                var color = HslToRgb(hue, 0.6f, 0.3f + t * 0.4f);
                for (var x = 0; x < capabilities.Width; x++)
                {
                    image[x, y] = color;
                }
            }
        });

        return image;
    }

    private static Image<Rgba32> GenerateAnimatedPattern(LcdCapabilities capabilities)
    {
        var image = new Image<Rgba32>(capabilities.Width, capabilities.Height);
        var time = DateTime.Now;

        // Generate a simple animated gradient pattern
        var hueOffset = (float)(time.Second * 6 + time.Millisecond / 1000.0 * 6); // 0-360 over minute

        image.Mutate(ctx =>
        {
            // Fill with gradient
            for (var y = 0; y < capabilities.Height; y++)
            {
                var t = (float)y / capabilities.Height;
                var hue = (hueOffset + t * 60) % 360;
                var color = HslToRgb(hue, 0.7f, 0.5f);

                for (var x = 0; x < capabilities.Width; x++)
                {
                    image[x, y] = color;
                }
            }
        });

        return image;
    }

    private static Rgba32 HslToRgb(float h, float s, float l)
    {
        float r, g, b;

        if (Math.Abs(s) < 0.001f)
        {
            r = g = b = l;
        }
        else
        {
            var q = l < 0.5f ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = HueToRgb(p, q, h / 360f + 1f / 3f);
            g = HueToRgb(p, q, h / 360f);
            b = HueToRgb(p, q, h / 360f - 1f / 3f);
        }

        return new Rgba32(
            (byte)(r * 255),
            (byte)(g * 255),
            (byte)(b * 255));
    }

    private static float HueToRgb(float p, float q, float t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1f / 6f) return p + (q - p) * 6 * t;
        if (t < 1f / 2f) return q;
        if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6;
        return p;
    }

    private void OnDeviceDiscovered(object? sender, LcdDeviceEventArgs e)
    {
        _logger.LogInformation("New device discovered: {Device}", e.Device.Info);

        Task.Run(async () =>
        {
            try
            {
                await e.Device.ConnectAsync();
                _connectedDevices[e.Device.Info.UniqueId] = e.Device;
                _logger.LogInformation("Connected to newly discovered {DeviceName}", e.Device.Info.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to discovered device {DeviceName}", e.Device.Info.Name);
            }
        });
    }

    private void OnDeviceDisconnected(object? sender, LcdDeviceEventArgs e)
    {
        _logger.LogInformation("Device disconnected: {Device}", e.Device.Info);
        _connectedDevices.Remove(e.Device.Info.UniqueId);
    }
}
