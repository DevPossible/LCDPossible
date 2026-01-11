using LCDPossible.Core.Caching;
using LCDPossible.Core.Configuration;
using LCDPossible.Core.Devices;
using LCDPossible.Core.Ipc;
using LCDPossible.Core.Monitoring;
using LCDPossible.Core.Plugins;
using LCDPossible.Core.Rendering;
using LCDPossible.Ipc;
using LCDPossible.Monitoring;
using LCDPossible.Panels;
using Microsoft.Extensions.Options;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible;

/// <summary>
/// Background worker that manages LCD devices and rendering.
/// </summary>
public sealed class LcdWorker : BackgroundService
{
    private readonly IHostApplicationLifetime _appLifetime;
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
    private readonly Dictionary<string, float> _brightnessLevels = []; // 0.0 to 1.0
    private readonly Dictionary<string, (Image<Rgba32> ErrorFrame, string ErrorMessage)> _panelErrorCache = [];
    private readonly HashSet<string> _failedPanels = [];

    private ISystemInfoProvider? _systemProvider;
    private IProxmoxProvider? _proxmoxProvider;
    private PanelFactory? _panelFactory;
    private DisplayProfile? _displayProfile;
    private string? _profilePath;
    private IpcServer? _ipcServer;
    private IpcCommandHandler? _ipcCommandHandler;

    public LcdWorker(
        IHostApplicationLifetime appLifetime,
        DeviceManager deviceManager,
        ProfileLoader profileLoader,
        PluginManager pluginManager,
        IOptions<LcdPossibleOptions> options,
        ILogger<LcdWorker> logger,
        ILoggerFactory loggerFactory)
    {
        _appLifetime = appLifetime ?? throw new ArgumentNullException(nameof(appLifetime));
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
        (_displayProfile, _profilePath) = _profileLoader.LoadProfileWithPath();
        _logger.LogInformation("Using display profile: {ProfileName} from {Path}",
            _displayProfile.Name, _profilePath ?? "(default)");

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

            // Start IPC server for CLI communication
            await StartIpcServerAsync(stoppingToken);

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
            // Stop IPC server
            await StopIpcServerAsync();

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

            // Cleanup error caches
            foreach (var cached in _panelErrorCache.Values)
            {
                cached.ErrorFrame.Dispose();
            }
            _panelErrorCache.Clear();
            _failedPanels.Clear();

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

                    // Apply software brightness adjustment
                    var brightness = GetBrightnessLevel(configKey);
                    ApplyBrightness(frame, brightness);

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
        IDisplayPanel? panel = null;
        var panelKey = configKey;

        if (_singlePanels.TryGetValue(configKey, out panel))
        {
            panelKey = configKey;
        }
        else if (_singlePanels.TryGetValue("default", out panel))
        {
            panelKey = "default";
        }

        if (panel == null)
        {
            return GenerateSolidColor(capabilities, new Rgba32(30, 30, 40));
        }

        // If this panel has previously failed, return the cached error frame
        if (_failedPanels.Contains(panelKey))
        {
            if (_panelErrorCache.TryGetValue(panelKey, out var errorCached))
            {
                return errorCached.ErrorFrame.Clone();
            }
        }

        try
        {
            return await panel.RenderFrameAsync(capabilities.Width, capabilities.Height, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Panel '{PanelId}' failed to render: {Message}", panel.PanelId, ex.Message);

            // Mark as failed and cache error page
            _failedPanels.Add(panelKey);
            var errorFrame = GenerateErrorPage(capabilities.Width, capabilities.Height, panel.PanelId, ex.Message);
            _panelErrorCache[panelKey] = (errorFrame, ex.Message);

            _logger.LogWarning("Panel '{PanelId}' marked as failed - displaying error page", panel.PanelId);

            return errorFrame.Clone();
        }
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

    /// <summary>
    /// Generates an error page image for display when a panel fails.
    /// </summary>
    private static Image<Rgba32> GenerateErrorPage(int width, int height, string panelName, string errorMessage)
    {
        var image = new Image<Rgba32>(width, height);

        // Dark red gradient background
        image.Mutate(ctx =>
        {
            ctx.BackgroundColor(new Rgba32(40, 10, 10));
        });

        // Try to load system font for error text
        Font? titleFont = null;
        Font? messageFont = null;
        Font? hintFont = null;

        try
        {
            if (SystemFonts.TryGet("Segoe UI", out var family) ||
                SystemFonts.TryGet("Arial", out family) ||
                SystemFonts.TryGet("DejaVu Sans", out family))
            {
                titleFont = family.CreateFont(28, FontStyle.Bold);
                messageFont = family.CreateFont(18, FontStyle.Regular);
                hintFont = family.CreateFont(14, FontStyle.Italic);
            }
        }
        catch
        {
            // Font loading failed, we'll render without text
        }

        if (titleFont != null && messageFont != null && hintFont != null)
        {
            var errorColor = new Rgba32(255, 100, 100);
            var textColor = new Rgba32(220, 220, 220);
            var hintColor = new Rgba32(150, 150, 150);

            image.Mutate(ctx =>
            {
                var y = height * 0.25f;

                // Error icon (simple X)
                var centerX = width / 2f;
                ctx.DrawLine(errorColor, 4f, new PointF(centerX - 30, y - 30), new PointF(centerX + 30, y + 30));
                ctx.DrawLine(errorColor, 4f, new PointF(centerX + 30, y - 30), new PointF(centerX - 30, y + 30));

                y += 60;

                // Title
                var title = "Panel Error";
                var titleOptions = new RichTextOptions(titleFont)
                {
                    Origin = new PointF(centerX, y),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                ctx.DrawText(titleOptions, title, errorColor);

                y += 50;

                // Panel name
                var panelText = $"Panel: {panelName}";
                var panelOptions = new RichTextOptions(messageFont)
                {
                    Origin = new PointF(centerX, y),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                ctx.DrawText(panelOptions, panelText, textColor);

                y += 35;

                // Error message (truncate if too long)
                var displayError = errorMessage.Length > 80
                    ? errorMessage[..77] + "..."
                    : errorMessage;
                var errorOptions = new RichTextOptions(messageFont)
                {
                    Origin = new PointF(centerX, y),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    WrappingLength = width - 40
                };
                ctx.DrawText(errorOptions, displayError, textColor);

                y += 60;

                // Hint
                var hint = "Panel disabled until restart";
                var hintOptions = new RichTextOptions(hintFont)
                {
                    Origin = new PointF(centerX, y),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                ctx.DrawText(hintOptions, hint, hintColor);
            });
        }

        return image;
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

    #region IPC Server

    private async Task StartIpcServerAsync(CancellationToken cancellationToken)
    {
        try
        {
            _ipcServer = new IpcServer(_loggerFactory.CreateLogger<IpcServer>());
            _ipcCommandHandler = new IpcCommandHandler(
                _appLifetime,
                _deviceManager,
                _panelFactory,
                () => _connectedDevices,
                () => _slideshows,
                () => _displayProfile,
                () => _profilePath,
                SetSlideshowAsync,
                SetStaticImageAsync,
                SetBrightnessAsync,
                ReloadProfileAsync,
                _loggerFactory.CreateLogger<IpcCommandHandler>());

            _ipcServer.RequestReceived += OnIpcRequest;
            await _ipcServer.StartAsync(cancellationToken);

            _logger.LogInformation("IPC server started at {Path}", IpcPaths.GetFullPipePath());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start IPC server. CLI commands will not work while service is running.");
        }
    }

    private async Task StopIpcServerAsync()
    {
        if (_ipcServer is null)
            return;

        try
        {
            _ipcServer.RequestReceived -= OnIpcRequest;
            await _ipcServer.StopAsync(CancellationToken.None);
            _ipcServer.Dispose();
            _ipcServer = null;
            _ipcCommandHandler = null;
            _logger.LogInformation("IPC server stopped");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping IPC server");
        }
    }

    private async void OnIpcRequest(object? sender, IpcRequestEventArgs e)
    {
        if (_ipcCommandHandler is null)
        {
            await e.SendResponseAsync(
                IpcResponse.Fail(e.Request.Id, IpcErrorCodes.InternalError, "Command handler not initialized"),
                CancellationToken.None);
            return;
        }

        try
        {
            var response = await _ipcCommandHandler.HandleAsync(e.Request, CancellationToken.None);
            await e.SendResponseAsync(response, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling IPC request");
            await e.SendResponseAsync(
                IpcResponse.Fail(e.Request.Id, ex),
                CancellationToken.None);
        }
    }

    /// <summary>
    /// Reloads the display profile from disk and reinitializes the slideshow.
    /// Called via IPC when profile is modified by CLI commands.
    /// </summary>
    internal async Task ReloadProfileAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reloading display profile...");

        // Clear the asset cache to force re-download of URL-based content
        AssetCache.Clear();
        _logger.LogDebug("Cleared asset cache");

        // Reload profile from disk
        var (newProfile, newPath) = _profileLoader.LoadProfileWithPath();
        _displayProfile = newProfile;
        _profilePath = newPath;
        _logger.LogInformation("Loaded profile '{ProfileName}' with {Count} slides from {Path}",
            newProfile.Name, newProfile.Slides.Count, newPath ?? "(default)");

        // Update panel factory color scheme
        if (_panelFactory != null)
        {
            _panelFactory.SetColorScheme(newProfile.Colors);
        }

        // Dispose old default slideshow
        if (_slideshows.TryGetValue("default", out var oldSlideshow))
        {
            oldSlideshow.Dispose();
            _slideshows.Remove("default");
        }

        // Clear any cached errors for panels that might now work
        _failedPanels.Clear();
        foreach (var cached in _panelErrorCache.Values)
        {
            cached.ErrorFrame.Dispose();
        }
        _panelErrorCache.Clear();

        // Reinitialize default slideshow
        await InitializeDefaultSlideshowAsync(cancellationToken);

        _logger.LogInformation("Profile reload complete");
    }

    /// <summary>
    /// Sets or replaces the slideshow for a device/config key.
    /// </summary>
    internal Task SetSlideshowAsync(string configKey, SlideshowManager slideshow, CancellationToken cancellationToken)
    {
        // Dispose old slideshow if exists
        if (_slideshows.TryGetValue(configKey, out var oldSlideshow))
        {
            oldSlideshow.Dispose();
        }

        _slideshows[configKey] = slideshow;
        _logger.LogInformation("Slideshow updated for {ConfigKey}", configKey);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sets a static image for a device/config key.
    /// </summary>
    internal Task SetStaticImageAsync(string configKey, Image<Rgba32>? image)
    {
        // Dispose old image if exists
        if (_staticImages.TryGetValue(configKey, out var oldImage))
        {
            oldImage?.Dispose();
        }

        _staticImages[configKey] = image;

        // When setting a static image, we should also update the device mode to "static"
        // For now, the image will be used when the device mode is already "static"
        // or we can override by temporarily replacing the slideshow

        _logger.LogInformation("Static image set for {ConfigKey}", configKey);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sets the software brightness level for a device/config key.
    /// Note: This is software brightness (image adjustment), not hardware brightness.
    /// The Trofeo Vision LCD does not support hardware brightness control.
    /// </summary>
    /// <param name="configKey">Device config key or "default".</param>
    /// <param name="level">Brightness level 0-100 (50 = normal, 0 = black, 100 = max brightness).</param>
    internal Task SetBrightnessAsync(string configKey, int level)
    {
        // Convert 0-100 to 0.0-1.0 range for ImageSharp
        // 50 = 0.5 = normal brightness
        // 0 = 0.0 = black
        // 100 = 1.0 = maximum brightness
        var normalizedLevel = Math.Clamp(level / 100f, 0f, 1f);
        _brightnessLevels[configKey] = normalizedLevel;

        _logger.LogInformation("Software brightness set to {Level}% for {ConfigKey}", level, configKey);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the current brightness level for a device/config key.
    /// </summary>
    internal float GetBrightnessLevel(string configKey)
    {
        if (_brightnessLevels.TryGetValue(configKey, out var level))
        {
            return level;
        }
        if (_brightnessLevels.TryGetValue("default", out level))
        {
            return level;
        }
        return 0.5f; // Default: 50% (normal brightness)
    }

    /// <summary>
    /// Applies software brightness adjustment to an image.
    /// </summary>
    private void ApplyBrightness(Image<Rgba32> image, float brightness)
    {
        // brightness is 0.0 to 1.0, where 0.5 is normal
        // ImageSharp Brightness uses -1 to 1 where 0 is unchanged
        // Convert: 0.0 -> -1.0, 0.5 -> 0.0, 1.0 -> 1.0
        var adjustment = (brightness - 0.5f) * 2f;

        if (Math.Abs(adjustment) > 0.01f)
        {
            image.Mutate(ctx => ctx.Brightness(1f + adjustment));
        }
    }

    #endregion
}
