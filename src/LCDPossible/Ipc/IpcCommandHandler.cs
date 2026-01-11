using System.Reflection;
using System.Text.Json;
using LCDPossible.Cli;
using LCDPossible.Core.Configuration;
using LCDPossible.Core.Devices;
using LCDPossible.Core.Ipc;
using LCDPossible.Core.Rendering;
using LCDPossible.Panels;
using Microsoft.Extensions.Hosting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LCDPossible.Ipc;

/// <summary>
/// Handles IPC commands from CLI clients by delegating to the appropriate service operations.
/// </summary>
public sealed class IpcCommandHandler
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly DeviceManager _deviceManager;
    private readonly PanelFactory? _panelFactory;
    private readonly Func<Dictionary<string, ILcdDevice>> _getConnectedDevices;
    private readonly Func<Dictionary<string, SlideshowManager>> _getSlideshows;
    private readonly Func<DisplayProfile?> _getDisplayProfile;
    private readonly Func<string?> _getProfilePath;
    private readonly Func<string, SlideshowManager, CancellationToken, Task> _setSlideshowAsync;
    private readonly Func<string, Image<Rgba32>?, Task> _setStaticImageAsync;
    private readonly Func<string, int, Task> _setBrightnessAsync;
    private readonly Func<CancellationToken, Task> _reloadProfileAsync;
    private readonly ILogger<IpcCommandHandler> _logger;

    public IpcCommandHandler(
        IHostApplicationLifetime appLifetime,
        DeviceManager deviceManager,
        PanelFactory? panelFactory,
        Func<Dictionary<string, ILcdDevice>> getConnectedDevices,
        Func<Dictionary<string, SlideshowManager>> getSlideshows,
        Func<DisplayProfile?> getDisplayProfile,
        Func<string?> getProfilePath,
        Func<string, SlideshowManager, CancellationToken, Task> setSlideshowAsync,
        Func<string, Image<Rgba32>?, Task> setStaticImageAsync,
        Func<string, int, Task> setBrightnessAsync,
        Func<CancellationToken, Task> reloadProfileAsync,
        ILogger<IpcCommandHandler> logger)
    {
        _appLifetime = appLifetime;
        _deviceManager = deviceManager;
        _panelFactory = panelFactory;
        _getConnectedDevices = getConnectedDevices;
        _getSlideshows = getSlideshows;
        _getDisplayProfile = getDisplayProfile;
        _getProfilePath = getProfilePath;
        _setSlideshowAsync = setSlideshowAsync;
        _setStaticImageAsync = setStaticImageAsync;
        _setBrightnessAsync = setBrightnessAsync;
        _reloadProfileAsync = reloadProfileAsync;
        _logger = logger;
    }

    /// <summary>
    /// Handles an IPC request and returns the appropriate response.
    /// </summary>
    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing IPC command: {Command}", request.Command);

        try
        {
            return request.Command.ToLowerInvariant() switch
            {
                "status" => HandleStatus(request),
                "show" => await HandleShowAsync(request, cancellationToken),
                "set-image" => await HandleSetImageAsync(request, cancellationToken),
                "test-pattern" => await HandleTestPatternAsync(request, cancellationToken),
                "set-brightness" => await HandleSetBrightnessAsync(request, cancellationToken),
                "profile" => await HandleProfileAsync(request, cancellationToken),
                "profile-info" => HandleProfileInfo(request),
                "reload" or "reload-profile" => await HandleReloadAsync(request, cancellationToken),
                "next" => HandleNext(request),
                "previous" => HandlePrevious(request),
                "stop" => HandleStop(request),
                "list" => HandleList(request),
                _ => IpcResponse.Fail(request.Id, IpcErrorCodes.InvalidCommand,
                    $"Unknown command: {request.Command}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling IPC command {Command}", request.Command);
            return IpcResponse.Fail(request.Id, ex);
        }
    }

    private IpcResponse HandleStatus(IpcRequest request)
    {
        var devices = _getConnectedDevices();
        var slideshows = _getSlideshows();
        var profile = _getDisplayProfile();

        // Get current slideshow status from "default" slideshow
        SlideshowStatus? slideshowStatus = null;
        if (slideshows.TryGetValue("default", out var defaultSlideshow))
        {
            slideshowStatus = new SlideshowStatus
            {
                CurrentIndex = defaultSlideshow.CurrentIndex,
                TotalSlides = defaultSlideshow.TotalSlides,
                CurrentPanel = defaultSlideshow.CurrentPanelId,
                SecondsRemaining = (int)defaultSlideshow.TimeRemaining.TotalSeconds
            };
        }

        var status = new ServiceStatus
        {
            Version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0",
            ProfileName = profile?.Name,
            IsRunning = true,
            ConnectedDevices = devices.Count,
            Devices = devices.Values.Select(d => new DeviceStatus
            {
                UniqueId = d.Info.UniqueId,
                Name = d.Info.Name,
                IsConnected = d.IsConnected,
                Width = d.Capabilities.Width,
                Height = d.Capabilities.Height,
                CurrentMode = "slideshow" // TODO: Track actual mode per device
            }).ToList(),
            CurrentSlideshow = slideshowStatus
        };

        return IpcResponse.Ok(request.Id, status);
    }

    private async Task<IpcResponse> HandleShowAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var panels = request.GetString("panels");
        if (string.IsNullOrWhiteSpace(panels))
        {
            return IpcResponse.Fail(request.Id, IpcErrorCodes.InvalidArgs, "Missing 'panels' argument");
        }

        // Validate panels
        var validation = InlineProfileParser.Validate(panels);
        if (!validation.IsValid)
        {
            return IpcResponse.Fail(request.Id, IpcErrorCodes.InvalidPanel,
                validation.Error ?? "Invalid panel specification");
        }

        if (_panelFactory == null)
        {
            return IpcResponse.Fail(request.Id, IpcErrorCodes.InternalError, "Panel factory not initialized");
        }

        // Parse panels and create new slideshow
        var items = InlineProfileParser.Parse(panels);
        if (items.Count == 0)
        {
            return IpcResponse.Fail(request.Id, IpcErrorCodes.InvalidArgs, "No valid panels specified");
        }

        var slideshow = new SlideshowManager(
            _panelFactory,
            items,
            _logger as ILogger<SlideshowManager> ??
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SlideshowManager>.Instance);

        await slideshow.InitializeAsync(cancellationToken);
        await _setSlideshowAsync("default", slideshow, cancellationToken);

        _logger.LogInformation("IPC: Changed slideshow to {Count} panels: {Panels}",
            items.Count, panels);

        return IpcResponse.Ok(request.Id, new { message = $"Slideshow updated with {items.Count} panel(s)" });
    }

    private async Task<IpcResponse> HandleSetImageAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var path = request.GetString("path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return IpcResponse.Fail(request.Id, IpcErrorCodes.InvalidArgs, "Missing 'path' argument");
        }

        if (!File.Exists(path))
        {
            return IpcResponse.Fail(request.Id, IpcErrorCodes.FileNotFound, $"File not found: {path}");
        }

        try
        {
            var image = await Image.LoadAsync<Rgba32>(path, cancellationToken);
            await _setStaticImageAsync("default", image);

            _logger.LogInformation("IPC: Set static image from {Path}", path);
            return IpcResponse.Ok(request.Id, new { message = $"Image set: {path}" });
        }
        catch (Exception ex)
        {
            return IpcResponse.Fail(request.Id, IpcErrorCodes.InternalError,
                $"Failed to load image: {ex.Message}");
        }
    }

    private async Task<IpcResponse> HandleTestPatternAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var deviceIndex = request.GetInt("device");
        var devices = _getConnectedDevices();

        if (devices.Count == 0)
        {
            return IpcResponse.Fail(request.Id, IpcErrorCodes.DeviceNotFound, "No devices connected");
        }

        // Generate test pattern
        var targetDevice = deviceIndex.HasValue && deviceIndex.Value < devices.Count
            ? devices.Values.ElementAt(deviceIndex.Value)
            : devices.Values.First();

        var testImage = GenerateTestPattern(targetDevice.Capabilities.Width, targetDevice.Capabilities.Height);
        await _setStaticImageAsync("default", testImage);

        _logger.LogInformation("IPC: Displayed test pattern on {Device}", targetDevice.Info.Name);
        return IpcResponse.Ok(request.Id, new { message = $"Test pattern displayed on {targetDevice.Info.Name}" });
    }

    private async Task<IpcResponse> HandleSetBrightnessAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var level = request.GetInt("level");
        if (!level.HasValue || level < 0 || level > 100)
        {
            return IpcResponse.Fail(request.Id, IpcErrorCodes.InvalidArgs,
                "Invalid brightness level (must be 0-100)");
        }

        // Note: This is SOFTWARE brightness (image gamma adjustment), not hardware brightness.
        // The Trofeo Vision LCD does not support hardware brightness control.
        // We apply brightness adjustment to frames before encoding.

        var deviceKey = request.GetString("device") ?? "default";
        await _setBrightnessAsync(deviceKey, level.Value);

        _logger.LogInformation("IPC: Set software brightness to {Level}%", level.Value);
        return IpcResponse.Ok(request.Id, new
        {
            message = $"Brightness set to {level.Value}% (software adjustment)"
        });
    }

    private async Task<IpcResponse> HandleProfileAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var path = request.GetString("path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return IpcResponse.Fail(request.Id, IpcErrorCodes.InvalidArgs, "Missing 'path' argument");
        }

        if (!File.Exists(path))
        {
            return IpcResponse.Fail(request.Id, IpcErrorCodes.FileNotFound, $"Profile not found: {path}");
        }

        // TODO: Implement profile loading from specific path
        // For now, use reload which loads from the default location
        return IpcResponse.Fail(request.Id, IpcErrorCodes.InternalError,
            "Profile loading via IPC not yet implemented. Use 'reload' to reload the current profile.");
    }

    private async Task<IpcResponse> HandleReloadAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _reloadProfileAsync(cancellationToken);

            var profile = _getDisplayProfile();
            _logger.LogInformation("IPC: Reloaded profile '{ProfileName}'", profile?.Name);

            return IpcResponse.Ok(request.Id, new
            {
                message = "Profile reloaded successfully",
                profileName = profile?.Name,
                slideCount = profile?.Slides.Count ?? 0
            });
        }
        catch (Exception ex)
        {
            return IpcResponse.Fail(request.Id, IpcErrorCodes.InternalError,
                $"Failed to reload profile: {ex.Message}");
        }
    }

    private IpcResponse HandleProfileInfo(IpcRequest request)
    {
        var profile = _getDisplayProfile();
        var profilePath = _getProfilePath();

        if (profile == null)
        {
            return IpcResponse.Fail(request.Id, IpcErrorCodes.InternalError, "No profile loaded");
        }

        // Return detailed profile info including path and all slides
        var slides = profile.Slides.Select((slide, index) => new
        {
            index,
            panel = slide.Panel,
            type = slide.Type,
            source = slide.Source,
            duration = slide.Duration,
            updateInterval = slide.UpdateInterval,
            background = slide.Background,
            transition = slide.Transition,
            transitionDurationMs = slide.TransitionDurationMs
        }).ToList();

        return IpcResponse.Ok(request.Id, new
        {
            name = profile.Name,
            description = profile.Description,
            path = profilePath,
            defaultDurationSeconds = profile.DefaultDurationSeconds,
            defaultUpdateIntervalSeconds = profile.DefaultUpdateIntervalSeconds,
            defaultTransition = profile.DefaultTransition,
            defaultTransitionDurationMs = profile.DefaultTransitionDurationMs,
            slideCount = profile.Slides.Count,
            slides
        });
    }

    private IpcResponse HandleNext(IpcRequest request)
    {
        var slideshows = _getSlideshows();
        if (!slideshows.TryGetValue("default", out var slideshow))
        {
            return IpcResponse.Fail(request.Id, IpcErrorCodes.InternalError, "No slideshow active");
        }

        slideshow.NextSlide();
        _logger.LogInformation("IPC: Advanced to next slide");

        return IpcResponse.Ok(request.Id, new
        {
            message = "Advanced to next slide",
            currentIndex = slideshow.CurrentIndex,
            currentPanel = slideshow.CurrentPanelId
        });
    }

    private IpcResponse HandlePrevious(IpcRequest request)
    {
        var slideshows = _getSlideshows();
        if (!slideshows.TryGetValue("default", out var slideshow))
        {
            return IpcResponse.Fail(request.Id, IpcErrorCodes.InternalError, "No slideshow active");
        }

        slideshow.PreviousSlide();
        _logger.LogInformation("IPC: Returned to previous slide");

        return IpcResponse.Ok(request.Id, new
        {
            message = "Returned to previous slide",
            currentIndex = slideshow.CurrentIndex,
            currentPanel = slideshow.CurrentPanelId
        });
    }

    private IpcResponse HandleStop(IpcRequest request)
    {
        _logger.LogInformation("IPC: Service stop requested");
        _appLifetime.StopApplication();

        return IpcResponse.Ok(request.Id, new { message = "Service stop initiated" });
    }

    private IpcResponse HandleList(IpcRequest request)
    {
        var devices = _getConnectedDevices();

        var deviceList = devices.Values.Select(d => new
        {
            uniqueId = d.Info.UniqueId,
            name = d.Info.Name,
            vendorId = d.Info.VendorId,
            productId = d.Info.ProductId,
            isConnected = d.IsConnected,
            width = d.Capabilities.Width,
            height = d.Capabilities.Height
        }).ToList();

        return IpcResponse.Ok(request.Id, new { devices = deviceList, count = deviceList.Count });
    }

    private static Image<Rgba32> GenerateTestPattern(int width, int height)
    {
        var image = new Image<Rgba32>(width, height);

        // Generate colorful test pattern with gradient bars
        var colors = new Rgba32[]
        {
            new(255, 0, 0),     // Red
            new(0, 255, 0),     // Green
            new(0, 0, 255),     // Blue
            new(255, 255, 0),   // Yellow
            new(255, 0, 255),   // Magenta
            new(0, 255, 255),   // Cyan
            new(255, 255, 255), // White
            new(128, 128, 128)  // Gray
        };

        var barHeight = height / colors.Length;

        for (var y = 0; y < height; y++)
        {
            var colorIndex = Math.Min(y / barHeight, colors.Length - 1);
            var baseColor = colors[colorIndex];

            for (var x = 0; x < width; x++)
            {
                // Add horizontal gradient within each bar
                var t = (float)x / width;
                var r = (byte)(baseColor.R * t);
                var g = (byte)(baseColor.G * t);
                var b = (byte)(baseColor.B * t);
                image[x, y] = new Rgba32(r, g, b);
            }
        }

        return image;
    }
}
