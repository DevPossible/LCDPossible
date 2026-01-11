using LCDPossible.Core.Configuration;
using LCDPossible.Core.Devices;
using LCDPossible.Core.Rendering;
using LCDPossible.Core.Usb;
using LCDPossible;
using LCDPossible.Cli;
using LCDPossible.Monitoring;
using LCDPossible.Panels;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// Check if we should run in service mode
var isServiceMode = ShouldRunAsService(args);

if (isServiceMode)
{
    return await RunServiceAsync(args);
}
else
{
    return await RunCliAsync(args);
}

static bool ShouldRunAsService(string[] args)
{
    // Running as Windows Service (via SCM)
    if (WindowsServiceHelpers.IsWindowsService())
        return true;

    // Explicit service command
    if (args.Length > 0)
    {
        var command = args[0].ToLowerInvariant().TrimStart('-', '/');
        return command is "serve" or "run" or "service";
    }

    return false;
}

static async Task<int> RunServiceAsync(string[] args)
{
    // Configure Serilog early for startup logging
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
        .CreateBootstrapLogger();

    try
    {
        Log.Information("Starting LCDPossible Service v{Version}", GetVersion());

        var builder = Host.CreateApplicationBuilder(args);

        // Configure Serilog from configuration
        builder.Services.AddSerilog((services, lc) => lc
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

        // Bind LCDPossible configuration section
        builder.Services.Configure<LcdPossibleOptions>(
            builder.Configuration.GetSection(LcdPossibleOptions.SectionName));

        // Register profile loader
        builder.Services.AddSingleton<ProfileLoader>();

        // Register core services
        builder.Services.AddSingleton<IDeviceEnumerator>(sp =>
            new HidSharpEnumerator(sp.GetService<ILoggerFactory>()));

        builder.Services.AddSingleton<DeviceManager>(sp =>
        {
            var enumerator = sp.GetRequiredService<IDeviceEnumerator>();
            var loggerFactory = sp.GetService<ILoggerFactory>();
            var manager = new DeviceManager(enumerator, loggerFactory);

            // Register all known drivers
            DriverRegistry.RegisterAllDrivers(manager, enumerator, loggerFactory);

            return manager;
        });

        // Add the background worker
        builder.Services.AddHostedService<LcdWorker>();

        // Enable Windows Service support (when running as service)
        if (WindowsServiceHelpers.IsWindowsService() || args.Contains("--service"))
        {
            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = "LCDPossible";
            });
        }

        var host = builder.Build();

        // Log startup mode
        var isWindowsService = WindowsServiceHelpers.IsWindowsService();
        Log.Information("Running in {Mode} mode", isWindowsService ? "Windows Service" : "Console");

        await host.RunAsync();
        return 0;
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Application terminated unexpectedly");
        return 1;
    }
    finally
    {
        await Log.CloseAndFlushAsync();
    }
}

static async Task<int> RunCliAsync(string[] args)
{
    if (args.Length == 0)
    {
        return ShowHelp();
    }

    var command = args[0].ToLowerInvariant().TrimStart('-', '/');

    return command switch
    {
        "list" => ListDevices(),
        "status" => ShowStatus(),
        "test" => await TestPattern(GetDeviceIndex(args)),
        "set-image" => await SetImage(args),
        "show" => await ShowPanels(args),
        "debug" => await DebugTest.RunAsync(),
        "show-profile" => ShowProfileInfo(args),
        "generate-profile" => GenerateSampleProfile(args),
        "help" or "h" or "?" => ShowHelp(),
        "version" or "v" => ShowVersion(),
        _ => UnknownCommand(command)
    };
}

static int ShowHelp()
{
    var version = GetVersion();

    Console.WriteLine($@"
LCDPossible v{version}
Cross-platform LCD controller for HID-based displays

USAGE:
    lcdpossible <command> [options]
    lcdpossible [--help | -h | /? | /h]

SERVICE COMMANDS:
    serve, run              Start the LCD service (foreground)
    serve --service         Start as Windows Service

CLI COMMANDS:
    list                    List connected LCD devices
    status                  Show status of connected devices and configuration
    test                    Display a test pattern on the LCD
    set-image               Send an image file to the LCD display
    show                    Quick display panels (inline profile format)
    show-profile            Show current or specified profile information
    generate-profile        Generate a sample profile YAML file

GENERAL OPTIONS:
    --help, -h, /?, /h      Show this help message
    --version, -v           Show version information

DEVICE OPTIONS:
    --device, -d <n>        Device index (default: 0, use 'list' to see devices)

IMAGE OPTIONS:
    --path, -p <file>       Path to image file (required for set-image)

PROFILE OPTIONS:
    --output, -o <file>     Output path for generate-profile command

SHOW COMMAND:
    Quick display of panels using inline profile format:
    Format: {{panel}}|@{{param}}={{value}}@{{param}}={{value}},{{panel}},...

    Parameters:
      @duration=N           How long to show this panel (seconds, default: 15)
      @interval=N           How often to refresh data (seconds, default: 5)
      @background=path      Background image for the panel

EXAMPLES:
    lcdpossible serve                         Start service in foreground
    lcdpossible list                          List all connected LCD devices
    lcdpossible test                          Send test pattern to first device
    lcdpossible test -d 1                     Send test pattern to second device
    lcdpossible set-image -p wallpaper.jpg    Display an image
    lcdpossible show basic-info               Show basic info panel
    lcdpossible show basic-info|@duration=10  Show with 10s duration
    lcdpossible show basic-info,cpu-usage-graphic   Show multiple panels
    lcdpossible show-profile                  Show loaded profile info
    lcdpossible show-profile c:\my-profile.yaml    Show specific profile
    lcdpossible generate-profile              Generate sample YAML to console
    lcdpossible generate-profile -o profile.yaml   Save sample to file

CONFIGURATION:
    LCDPossible uses a YAML profile for slideshow configuration.
    The profile is searched in these locations (first found wins):

    Windows:    C:\ProgramData\LCDPossible\display-profile.yaml
    Linux:      /etc/lcdpossible/display-profile.yaml
    macOS:      /Library/Application Support/LCDPossible/display-profile.yaml

    If no profile is found, a default profile is used.

AVAILABLE PANELS:
    System Information:
        basic-info          Hostname, OS, uptime summary
        os-info             Detailed OS information
        os-status           System status indicators
        os-notifications    System notifications

    CPU Panels:
        cpu-info            CPU model and specifications
        cpu-usage-text      CPU usage as text
        cpu-usage-graphic   CPU usage with visual bars

    GPU Panels:
        gpu-info            GPU model and specifications
        gpu-usage-text      GPU usage as text
        gpu-usage-graphic   GPU usage with visual bars

    Memory Panels:
        ram-info            RAM specifications
        ram-usage-text      RAM usage as text
        ram-usage-graphic   RAM usage with visual bars

    Proxmox (requires API configuration):
        proxmox-summary     Cluster overview
        proxmox-vms         VM/Container status list

    Media Panels:
        animated-gif:<path|url>     Animated GIF file or URL
        image-sequence:<folder>     Folder of numbered images (e.g., frame001.png)
        video:<path|url>            Video file, URL, or YouTube link
        html:<path>                 Local HTML file rendered as web page
        web:<url>                   Live website rendered from URL

MEDIA PANEL EXAMPLES:
    # Animated GIF (local file or URL)
    lcdpossible show animated-gif:C:\gifs\animation.gif
    lcdpossible show animated-gif:https://upload.wikimedia.org/wikipedia/commons/2/2c/Rotating_earth_%28large%29.gif

    # Image sequence (folder of numbered images at 30fps)
    lcdpossible show image-sequence:C:\frames\

    # Video (local file, direct URL, or YouTube)
    lcdpossible show video:C:\videos\demo.mp4
    lcdpossible show video:https://archive.org/download/BigBuckBunny_124/Content/big_buck_bunny_720p_surround.mp4
    lcdpossible show video:https://www.youtube.com/watch?v=aqz-KE-bpKQ

    # HTML panel (local HTML file)
    lcdpossible show html:C:\dashboard\status.html

    # Web panel (live website)
    lcdpossible show web:https://wttr.in/London

SUPPORTED DEVICES:
");
    foreach (var supported in DriverRegistry.SupportedDevices)
    {
        Console.WriteLine($"    {supported.DeviceName,-35} VID:0x{supported.VendorId:X4} PID:0x{supported.ProductId:X4}");
    }

    Console.WriteLine(@"
For more information, visit: https://github.com/DevPossible/LCDPossible
");
    return 0;
}

static int ShowVersion()
{
    var version = GetVersion();
    Console.WriteLine($"LCDPossible version {version}");
    return 0;
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    Console.Error.WriteLine("Use 'lcdpossible --help' for usage information.");
    return 1;
}

static int GetDeviceIndex(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--device" or "-d" && int.TryParse(args[i + 1], out var index))
        {
            return index;
        }
    }
    return 0;
}

static string? GetImagePath(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--path" or "-p")
        {
            return args[i + 1];
        }
    }
    return null;
}

static string? GetOutputPath(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--output" or "-o")
        {
            return args[i + 1];
        }
    }
    return null;
}

static string? GetShowProfile(string[] args)
{
    // For "show" command, the profile can be passed as:
    // 1. lcdpossible show basic-info (second arg is the profile)
    // 2. lcdpossible -show basic-info (first arg is -show, second is profile)
    // 3. lcdpossible show -p basic-info (using -p flag)

    // Check for explicit -p or --profile flag first
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--profile" or "-p")
        {
            return args[i + 1];
        }
    }

    // Otherwise, take the argument after "show"
    for (var i = 0; i < args.Length - 1; i++)
    {
        var arg = args[i].ToLowerInvariant().TrimStart('-', '/');
        if (arg == "show")
        {
            var nextArg = args[i + 1];
            // Make sure it's not another flag
            if (!nextArg.StartsWith("-") && !nextArg.StartsWith("/"))
            {
                return nextArg;
            }
        }
    }

    return null;
}

static async Task<int> ShowPanels(string[] args)
{
    var profile = GetShowProfile(args);
    var deviceIndex = GetDeviceIndex(args);

    if (string.IsNullOrEmpty(profile))
    {
        Console.Error.WriteLine("Error: No panels specified.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Usage: lcdpossible show <panels>");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Format: {panel}|@{param}={value}@{param}={value},{panel},...");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Examples:");
        Console.Error.WriteLine("  lcdpossible show basic-info");
        Console.Error.WriteLine("  lcdpossible show basic-info|@duration=10");
        Console.Error.WriteLine("  lcdpossible show basic-info|@duration=10@interval=5");
        Console.Error.WriteLine("  lcdpossible show basic-info,cpu-usage-graphic,gpu-usage-graphic");
        Console.Error.WriteLine("  lcdpossible show cpu-usage-graphic|@duration=15,ram-usage-graphic|@duration=10");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Parameters:");
        Console.Error.WriteLine("  @duration=N   How long to show this panel (seconds, default: 15)");
        Console.Error.WriteLine("  @interval=N   How often to refresh data (seconds, default: 5)");
        Console.Error.WriteLine("  @background=path  Background image for the panel");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Available panels:");
        foreach (var p in PanelFactory.AvailablePanels)
        {
            Console.Error.WriteLine($"  {p}");
        }
        return 1;
    }

    // Validate the profile
    var (isValid, error) = InlineProfileParser.Validate(profile);
    if (!isValid)
    {
        Console.Error.WriteLine($"Error: {error}");
        return 1;
    }

    // Parse the profile
    var items = InlineProfileParser.Parse(profile);
    Console.WriteLine($"Parsed {items.Count} panel(s) from inline profile");

    // Find device
    using var enumerator = new HidSharpEnumerator();
    using var deviceManager = new DeviceManager(enumerator);
    DriverRegistry.RegisterAllDrivers(deviceManager, enumerator);

    var devices = deviceManager.DiscoverDevices().ToList();

    if (devices.Count == 0)
    {
        Console.Error.WriteLine("Error: No LCD devices found.");
        return 1;
    }

    if (deviceIndex < 0 || deviceIndex >= devices.Count)
    {
        Console.Error.WriteLine($"Error: Invalid device index {deviceIndex}. Available: 0-{devices.Count - 1}");
        return 1;
    }

    var device = devices[deviceIndex];
    Console.WriteLine($"Connecting to: {device.Info.Name}");

    try
    {
        await device.ConnectAsync();
        Console.WriteLine("Connected!");

        // Create system info provider
        using var systemProvider = new LocalHardwareProvider();
        await systemProvider.InitializeAsync();

        // Create panel factory and slideshow manager
        var panelFactory = new PanelFactory(systemProvider);
        using var slideshow = new SlideshowManager(panelFactory, items);
        await slideshow.InitializeAsync();

        Console.WriteLine($"Running slideshow with {items.Count} panel(s)...");
        Console.WriteLine("Press any key to stop.\n");

        var encoder = new JpegImageEncoder { Quality = 95 };
        var cts = new CancellationTokenSource();

        // Run slideshow until key is pressed
        var keyTask = Task.Run(() =>
        {
            Console.ReadKey(intercept: true);
            cts.Cancel();
        });

        var frameInterval = TimeSpan.FromMilliseconds(1000.0 / 30); // 30 FPS
        var lastFrameTime = DateTime.UtcNow;

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var frame = await slideshow.RenderCurrentFrameAsync(
                    device.Capabilities.Width,
                    device.Capabilities.Height,
                    cts.Token);

                if (frame != null)
                {
                    var encoded = encoder.Encode(frame, device.Capabilities);
                    await device.SendFrameAsync(encoded, ColorFormat.Jpeg, cts.Token);
                    frame.Dispose();
                }

                // Maintain frame rate
                var elapsed = DateTime.UtcNow - lastFrameTime;
                var delay = frameInterval - elapsed;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cts.Token);
                }
                lastFrameTime = DateTime.UtcNow;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Console.WriteLine("\nStopped.");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
    finally
    {
        await device.DisconnectAsync();
    }
}

static int ListDevices()
{
    Console.WriteLine("Scanning for LCD devices...\n");

    using var enumerator = new HidSharpEnumerator();
    using var deviceManager = new DeviceManager(enumerator);

    DriverRegistry.RegisterAllDrivers(deviceManager, enumerator);

    var devices = deviceManager.DiscoverDevices().ToList();

    if (devices.Count == 0)
    {
        Console.WriteLine("No supported LCD devices found.");
        Console.WriteLine("\nSupported devices:");
        foreach (var supported in DriverRegistry.SupportedDevices)
        {
            Console.WriteLine($"  - {supported.DeviceName} (VID:0x{supported.VendorId:X4} PID:0x{supported.ProductId:X4})");
        }
        return 0;
    }

    Console.WriteLine($"Found {devices.Count} device(s):\n");
    for (var i = 0; i < devices.Count; i++)
    {
        var device = devices[i];
        Console.WriteLine($"[{i}] {device.Info.Name}");
        Console.WriteLine($"    Manufacturer: {device.Info.Manufacturer}");
        Console.WriteLine($"    VID:PID:      0x{device.Info.VendorId:X4}:0x{device.Info.ProductId:X4}");
        Console.WriteLine($"    Resolution:   {device.Capabilities.Width}x{device.Capabilities.Height}");
        Console.WriteLine($"    Driver:       {device.Info.DriverName}");
        Console.WriteLine();
    }

    return 0;
}

static int ShowStatus()
{
    Console.WriteLine("LCDPossible Status\n");
    Console.WriteLine($"Version:  {GetVersion()}");
    Console.WriteLine($"Platform: {Environment.OSVersion}");
    Console.WriteLine();

    return ListDevices();
}

static async Task<int> SetImage(string[] args)
{
    var imagePath = GetImagePath(args);
    var deviceIndex = GetDeviceIndex(args);

    if (string.IsNullOrEmpty(imagePath))
    {
        Console.Error.WriteLine("Error: --path is required for set-image command.");
        return 1;
    }

    if (!File.Exists(imagePath))
    {
        Console.Error.WriteLine($"Error: Image file not found: {imagePath}");
        return 1;
    }

    Console.WriteLine($"Loading image: {imagePath}");

    using var enumerator = new HidSharpEnumerator();
    using var deviceManager = new DeviceManager(enumerator);

    DriverRegistry.RegisterAllDrivers(deviceManager, enumerator);

    var devices = deviceManager.DiscoverDevices().ToList();

    if (devices.Count == 0)
    {
        Console.Error.WriteLine("Error: No LCD devices found.");
        return 1;
    }

    if (deviceIndex < 0 || deviceIndex >= devices.Count)
    {
        Console.Error.WriteLine($"Error: Invalid device index {deviceIndex}. Available: 0-{devices.Count - 1}");
        return 1;
    }

    var device = devices[deviceIndex];
    Console.WriteLine($"Connecting to: {device.Info.Name}");

    try
    {
        await device.ConnectAsync();
        Console.WriteLine("Connected!");

        using var image = await Image.LoadAsync<Rgba32>(imagePath);
        Console.WriteLine($"Image loaded: {image.Width}x{image.Height}");

        var encoder = new JpegImageEncoder { Quality = 95 };
        var encoded = encoder.Encode(image, device.Capabilities);
        Console.WriteLine($"Encoded to JPEG: {encoded.Length} bytes");

        await device.SendFrameAsync(encoded, ColorFormat.Jpeg);
        Console.WriteLine("Image sent successfully!");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
    finally
    {
        await device.DisconnectAsync();
    }
}

static async Task<int> TestPattern(int deviceIndex)
{
    Console.WriteLine("Generating test pattern...");

    using var enumerator = new HidSharpEnumerator();
    using var deviceManager = new DeviceManager(enumerator);

    DriverRegistry.RegisterAllDrivers(deviceManager, enumerator);

    var devices = deviceManager.DiscoverDevices().ToList();

    if (devices.Count == 0)
    {
        Console.Error.WriteLine("Error: No LCD devices found.");
        return 1;
    }

    if (deviceIndex < 0 || deviceIndex >= devices.Count)
    {
        Console.Error.WriteLine($"Error: Invalid device index {deviceIndex}. Available: 0-{devices.Count - 1}");
        return 1;
    }

    var device = devices[deviceIndex];
    Console.WriteLine($"Connecting to: {device.Info.Name}");

    try
    {
        await device.ConnectAsync();
        Console.WriteLine("Connected!");

        using var image = GenerateTestPattern(device.Capabilities.Width, device.Capabilities.Height);

        var encoder = new JpegImageEncoder { Quality = 95 };
        var encoded = encoder.Encode(image, device.Capabilities);
        Console.WriteLine($"Test pattern encoded: {encoded.Length} bytes");

        await device.SendFrameAsync(encoded, ColorFormat.Jpeg);
        Console.WriteLine("Test pattern sent successfully!");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
    finally
    {
        await device.DisconnectAsync();
    }
}

static Image<Rgba32> GenerateTestPattern(int width, int height)
{
    var image = new Image<Rgba32>(width, height);

    // Create a colorful gradient test pattern
    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {
            var r = (byte)(x * 255 / width);
            var g = (byte)(y * 255 / height);
            var b = (byte)128;
            image[x, y] = new Rgba32(r, g, b);
        }
    }

    return image;
}

static int ShowProfileInfo(string[] args)
{
    Console.WriteLine("Display Profile Information\n");

    // Check if a specific profile path was provided
    string? profilePath = GetProfileFilePath(args);
    DisplayProfile profile;

    if (!string.IsNullOrEmpty(profilePath))
    {
        // Load from specified file
        if (!File.Exists(profilePath))
        {
            Console.Error.WriteLine($"Error: Profile file not found: {profilePath}");
            return 1;
        }

        Console.WriteLine($"Loading profile from: {profilePath}\n");
        var loader = new ProfileLoader();
        profile = loader.LoadProfileFromFile(profilePath);
    }
    else
    {
        // Show search paths and load default
        Console.WriteLine("Profile search paths (in order):");
        foreach (var path in ProfileLoader.GetProfileSearchPaths())
        {
            var exists = File.Exists(path);
            var marker = exists ? "[FOUND]" : "[not found]";
            Console.WriteLine($"  {marker} {path}");
        }
        Console.WriteLine();

        // Load and display profile
        var loader = new ProfileLoader();
        profile = loader.LoadProfile();
    }

    Console.WriteLine($"Active Profile: {profile.Name}");
    if (!string.IsNullOrEmpty(profile.Description))
    {
        Console.WriteLine($"Description:    {profile.Description}");
    }
    Console.WriteLine($"\nDefault Settings:");
    Console.WriteLine($"  Update Interval: {profile.DefaultUpdateIntervalSeconds} second(s)");
    Console.WriteLine($"  Panel Duration:  {profile.DefaultDurationSeconds} second(s)");

    Console.WriteLine($"\nSlides ({profile.Slides.Count}):");
    for (var i = 0; i < profile.Slides.Count; i++)
    {
        var slide = profile.Slides[i];
        var type = slide.Type ?? "panel";
        var source = type == "image" ? slide.Source : (slide.Panel ?? slide.Source ?? "unknown");
        var duration = slide.Duration ?? profile.DefaultDurationSeconds;
        var updateInterval = slide.UpdateInterval ?? profile.DefaultUpdateIntervalSeconds;

        Console.WriteLine($"  [{i + 1}] {type}: {source}");
        Console.WriteLine($"      Duration: {duration}s, Update: {updateInterval}s");
        if (!string.IsNullOrEmpty(slide.Background))
        {
            Console.WriteLine($"      Background: {slide.Background}");
        }
    }

    return 0;
}

static string? GetProfileFilePath(string[] args)
{
    // For "show-profile" command, check for path after the command
    // lcdpossible show-profile c:\temp\profile.yaml
    for (var i = 0; i < args.Length - 1; i++)
    {
        var arg = args[i].ToLowerInvariant().TrimStart('-', '/');
        if (arg == "show-profile")
        {
            var nextArg = args[i + 1];
            // Make sure it's not another flag
            if (!nextArg.StartsWith("-") && !nextArg.StartsWith("/"))
            {
                return nextArg;
            }
        }
    }
    return null;
}

static int GenerateSampleProfile(string[] args)
{
    var outputPath = GetOutputPath(args);
    var yaml = ProfileLoader.GenerateSampleProfileYaml();

    if (string.IsNullOrEmpty(outputPath))
    {
        Console.WriteLine("# Sample LCDPossible Display Profile");
        Console.WriteLine("# Copy to your system config directory:");
        Console.WriteLine($"#   Windows: {Path.Combine(ProfileLoader.GetSystemConfigDirectory(), ProfileLoader.DefaultProfileFileName)}");
        Console.WriteLine("#   Linux:   /etc/lcdpossible/display-profile.yaml");
        Console.WriteLine("#   macOS:   /Library/Application Support/LCDPossible/display-profile.yaml");
        Console.WriteLine();
        Console.WriteLine(yaml);
    }
    else
    {
        File.WriteAllText(outputPath, yaml);
        Console.WriteLine($"Sample profile saved to: {outputPath}");
    }

    return 0;
}

static string GetVersion()
{
    var assembly = typeof(Program).Assembly;
    var version = assembly.GetName().Version;
    return version?.ToString(3) ?? "0.0.0";
}
