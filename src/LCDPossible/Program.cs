using System.Text.Json;
using LCDPossible.Core.Configuration;
using LCDPossible.Core.Devices;
using LCDPossible.Core.Ipc;
using LCDPossible.Core.Plugins;
using LCDPossible.Core.Rendering;
using LCDPossible.Core.Usb;
using LCDPossible;
using LCDPossible.Cli;
using LCDPossible.Ipc;
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

        // Register plugin manager
        builder.Services.AddSingleton<PluginManager>(sp =>
            new PluginManager(sp.GetService<ILoggerFactory>(), sp));

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
    // Find the first non-flag argument as the command
    var command = args.FirstOrDefault(a => !a.StartsWith("-") && !a.StartsWith("/"));

    if (string.IsNullOrEmpty(command))
    {
        // No command specified - show help
        return ShowHelp();
    }

    command = command.ToLowerInvariant();

    // Commands that should be routed through IPC when service is running
    var ipcCommands = new[] { "show", "set-image", "test-pattern", "status", "set-brightness", "next", "previous", "stop" };

    // Check if service is running and this is an IPC-capable command
    if (ipcCommands.Contains(command) && IpcPaths.IsServiceRunning())
    {
        return await RunViaIpcAsync(command, args);
    }

    return command switch
    {
        "list" => ListDevices(),
        "list-panels" or "panels" => ListPanels(),
        "help-panel" or "panel-help" => ShowPanelHelp(args),
        "status" => ShowStatus(),
        "test" => await RenderPanelsToFiles(args),
        "test-pattern" => await TestPattern(GetDeviceIndex(args)),
        "set-image" => await SetImage(args),
        "show" => await ShowPanels(args),
        "debug" => await DebugTest.RunAsync(),
        "sensors" or "list-sensors" => await RunSensorsCommand(),
        "profile" => ProfileCommands.Run(args),
        "service" => ServiceCommands.Run(args),
        "help" or "h" or "?" => ShowHelp(),
        "version" or "v" => ShowVersion(),
        "stop" => StopService(),
        "next" => AdvanceSlide(),
        "previous" => PreviousSlide(),
        "set-brightness" => await SetBrightness(args),
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
    service <sub-command>   Manage service installation (install/remove/start/stop/restart)

CLI COMMANDS:
    list                    List connected LCD devices
    list-panels, panels     List all available panel types with descriptions
    help-panel <type>       Show detailed help for a specific panel type
    status                  Show status of connected devices and configuration
    test                    Render panels to JPEG files (supports wildcards: *, ?)
    test-pattern            Display a test pattern on the LCD
    set-image               Send an image file to the LCD display
    show                    Quick display panels (uses default profile if no panels specified)
    profile <sub-command>   Manage display profiles (use 'profile help' for details)

RUNTIME COMMANDS (when service is running):
    status                  Get service status and current slideshow info
    show <panels>           Change the current slideshow panels
    set-image -p <file>     Display a static image
    test-pattern            Display a test pattern
    set-brightness <0-100>  Set display brightness
    next                    Advance to next slide
    previous                Go to previous slide
    stop                    Stop the service gracefully

    Note: When the service is running, these commands communicate via IPC.
    When the service is not running, they operate directly on the device.

GENERAL OPTIONS:
    --help, -h, /?, /h      Show this help message
    --version, -v           Show version information

DEVICE OPTIONS:
    --device, -d <n>        Device index (default: 0, use 'list' to see devices)

IMAGE OPTIONS:
    --path, -p <file>       Path to image file (required for set-image)

PROFILE COMMANDS:
    Use 'lcdpossible profile help' for full profile management documentation.
    Quick examples:
        lcdpossible profile new my-profile
        lcdpossible profile append-panel cpu-usage-graphic
        lcdpossible profile list-panels

SHOW COMMAND:
    Quick display of panels using inline profile format:
    Format: {{panel}}|@{{param}}={{value}}@{{param}}={{value}},{{panel}},...

    Parameters:
      @duration=N           How long to show this panel (seconds, default: 15)
      @interval=N           How often to refresh data (seconds, default: 5)
      @background=path      Background image for the panel

EXAMPLES:
    lcdpossible service install               Install service (requires admin/sudo)
    lcdpossible service start                 Start the installed service
    lcdpossible service status                Check service status
    lcdpossible service restart               Restart the service
    lcdpossible serve                         Start service in foreground
    lcdpossible list                          List all connected LCD devices
    lcdpossible list-panels                   List all available panel types
    lcdpossible help-panel video              Show help for the video panel
    lcdpossible test                          Render default panels to ~/panel.jpg files
    lcdpossible test cpu-info,gpu-info        Render specific panels to files
    lcdpossible test cpu-*                    Render all CPU panels (wildcard)
    lcdpossible test *-graphic                Render all graphic panels (wildcard)
    lcdpossible test *                        Render ALL available panels
    lcdpossible test-pattern                  Send test pattern to first device
    lcdpossible test-pattern -d 1             Send test pattern to second device
    lcdpossible set-image -p wallpaper.jpg    Display an image
    lcdpossible show                          Show default panels (basic, CPU, GPU, RAM)
    lcdpossible show basic-info               Show basic info panel
    lcdpossible show basic-info|@duration=10  Show with 10s duration
    lcdpossible show basic-info,cpu-usage-graphic   Show multiple panels
    lcdpossible profile list                  List available profiles
    lcdpossible profile new my-profile        Create a new profile
    lcdpossible profile append-panel cpu-info Add a panel to a profile
    lcdpossible profile list-panels           Show panels in default profile

CONFIGURATION:
    LCDPossible uses a YAML profile for slideshow configuration.
    The profile is searched in these locations (first found wins):

    Windows:    C:\ProgramData\LCDPossible\display-profile.yaml
    Linux:      /etc/lcdpossible/display-profile.yaml
    macOS:      /Library/Application Support/LCDPossible/display-profile.yaml

    If no profile is found, a default profile is used.
");

    // Dynamic panel list from plugins
    ShowAvailablePanelsSummary();

    Console.WriteLine(@"
    Use 'lcdpossible list-panels' for full descriptions.
    Use 'lcdpossible help-panel <type>' for detailed help on a panel.

SUPPORTED DEVICES:");
    foreach (var supported in DriverRegistry.SupportedDevices)
    {
        Console.WriteLine($"    {supported.DeviceName,-35} VID:0x{supported.VendorId:X4} PID:0x{supported.ProductId:X4}");
    }

    Console.WriteLine(@"
For more information, visit: https://github.com/DevPossible/LCDPossible
");
    return 0;
}

static void ShowAvailablePanelsSummary()
{
    Console.WriteLine("AVAILABLE PANELS:");

    try
    {
        using var pluginManager = new PluginManager();
        pluginManager.DiscoverPlugins();

        var panelFactory = new PanelFactory(pluginManager);
        var panelsByCategory = panelFactory.GetAllPanelMetadataByCategory();

        if (panelsByCategory.Count == 0)
        {
            Console.WriteLine("    No panels available. Make sure plugins are installed.");
            return;
        }

        // Define category order for consistent display
        var categoryOrder = new[] { "System", "CPU", "GPU", "Memory", "Network", "Storage", "Proxmox", "Media", "Web", "Other" };

        foreach (var category in categoryOrder)
        {
            if (!panelsByCategory.TryGetValue(category, out var panels) || panels.Count == 0)
                continue;

            Console.WriteLine($"    {category}:");
            foreach (var panel in panels)
            {
                Console.WriteLine($"        {panel.DisplayId}");
            }
            Console.WriteLine();
        }

        // Show any categories not in the predefined order
        foreach (var (category, panels) in panelsByCategory)
        {
            if (categoryOrder.Contains(category, StringComparer.OrdinalIgnoreCase))
                continue;

            Console.WriteLine($"    {category}:");
            foreach (var panel in panels)
            {
                Console.WriteLine($"        {panel.DisplayId}");
            }
            Console.WriteLine();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    Error loading panels: {ex.Message}");
    }
}

static int ShowVersion()
{
    var version = GetVersion();
    Console.WriteLine($"LCDPossible version {version}");
    return 0;
}

static int ListPanels()
{
    Console.WriteLine("Available Panels\n");

    using var pluginManager = new PluginManager();
    pluginManager.DiscoverPlugins();

    var panelFactory = new PanelFactory(pluginManager);
    var panelsByCategory = panelFactory.GetAllPanelMetadataByCategory();

    if (panelsByCategory.Count == 0)
    {
        Console.WriteLine("No panels available.");
        Console.WriteLine("Make sure plugins are installed in the plugins directory.");
        return 0;
    }

    // Define category order for consistent display
    var categoryOrder = new[] { "System", "CPU", "GPU", "Memory", "Network", "Storage", "Proxmox", "Media", "Web", "Other" };

    foreach (var category in categoryOrder)
    {
        if (!panelsByCategory.TryGetValue(category, out var panels) || panels.Count == 0)
            continue;

        Console.WriteLine($"{category}:");
        foreach (var panel in panels)
        {
            var typeId = panel.DisplayId;
            var description = panel.Description ?? "No description available";

            // Truncate description if too long
            const int maxDescLen = 55;
            if (description.Length > maxDescLen)
            {
                description = description[..(maxDescLen - 3)] + "...";
            }

            // Format: type ID padded, then description
            Console.WriteLine($"    {typeId,-28} {description}");
        }
        Console.WriteLine();
    }

    // Show any categories not in the predefined order
    foreach (var (category, panels) in panelsByCategory)
    {
        if (categoryOrder.Contains(category, StringComparer.OrdinalIgnoreCase))
            continue;

        Console.WriteLine($"{category}:");
        foreach (var panel in panels)
        {
            var typeId = panel.DisplayId;
            var description = panel.Description ?? "No description available";
            Console.WriteLine($"    {typeId,-28} {description}");
        }
        Console.WriteLine();
    }

    Console.WriteLine("Use 'lcdpossible help-panel <panel-type>' for detailed help on a specific panel.");
    return 0;
}

static int ShowPanelHelp(string[] args)
{
    // Find panel type ID from args
    string? panelTypeId = null;
    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i].ToLowerInvariant().TrimStart('-', '/');
        if (arg is "help-panel" or "panel-help")
        {
            if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
            {
                panelTypeId = args[i + 1];
            }
            break;
        }
    }

    if (string.IsNullOrWhiteSpace(panelTypeId))
    {
        Console.Error.WriteLine("Usage: lcdpossible help-panel <panel-type>");
        Console.Error.WriteLine("Example: lcdpossible help-panel video");
        Console.Error.WriteLine("\nUse 'lcdpossible list-panels' to see all available panels.");
        return 1;
    }

    using var pluginManager = new PluginManager();
    pluginManager.DiscoverPlugins();

    var panelFactory = new PanelFactory(pluginManager);
    var metadata = panelFactory.GetPanelMetadata(panelTypeId);

    if (metadata == null)
    {
        Console.Error.WriteLine($"Error: Unknown panel type '{panelTypeId}'");
        Console.Error.WriteLine("\nUse 'lcdpossible list-panels' to see all available panels.");
        return 1;
    }

    // Display panel header
    Console.WriteLine($"\n{metadata.DisplayName ?? metadata.DisplayId}");
    Console.WriteLine(new string('=', (metadata.DisplayName ?? metadata.DisplayId).Length));
    Console.WriteLine();

    // Category and type info
    if (metadata.Category != null)
    {
        Console.WriteLine($"Category:  {metadata.Category}");
    }
    Console.WriteLine($"Type ID:   {metadata.DisplayId}");

    // Flags
    var flags = new List<string>();
    if (metadata.IsLive) flags.Add("Live Data");
    if (metadata.IsAnimated) flags.Add("Animated");
    if (flags.Count > 0)
    {
        Console.WriteLine($"Features:  {string.Join(", ", flags)}");
    }
    Console.WriteLine();

    // Description
    if (!string.IsNullOrWhiteSpace(metadata.Description))
    {
        Console.WriteLine("DESCRIPTION:");
        Console.WriteLine($"    {metadata.Description}");
        Console.WriteLine();
    }

    // Detailed help text
    if (!string.IsNullOrWhiteSpace(metadata.HelpText))
    {
        Console.WriteLine("DETAILS:");
        // Indent each line of help text
        foreach (var line in metadata.HelpText.Split('\n'))
        {
            Console.WriteLine($"    {line.TrimEnd('\r')}");
        }
        Console.WriteLine();
    }

    // Parameters (for parameterized panels)
    if (metadata.Parameters?.Count > 0)
    {
        Console.WriteLine("PARAMETERS:");
        foreach (var param in metadata.Parameters)
        {
            var required = param.Required ? " (required)" : " (optional)";
            Console.WriteLine($"    {param.Name}{required}");
            Console.WriteLine($"        {param.Description}");

            if (param.DefaultValue != null)
            {
                Console.WriteLine($"        Default: {param.DefaultValue}");
            }

            if (param.ExampleValues?.Count > 0)
            {
                Console.WriteLine($"        Examples: {string.Join(", ", param.ExampleValues)}");
            }
            Console.WriteLine();
        }
    }

    // Examples
    if (metadata.Examples?.Count > 0)
    {
        Console.WriteLine("EXAMPLES:");
        foreach (var example in metadata.Examples)
        {
            Console.WriteLine($"    {example.Command}");
            Console.WriteLine($"        {example.Description}");
            Console.WriteLine();
        }
    }
    else
    {
        // Generate basic example if none provided
        Console.WriteLine("EXAMPLES:");
        Console.WriteLine($"    lcdpossible show {metadata.DisplayId}");
        Console.WriteLine($"        Display using the {metadata.DisplayName ?? metadata.DisplayId} panel");
        Console.WriteLine();
    }

    // Dependencies
    if (metadata.Dependencies?.Count > 0)
    {
        Console.WriteLine("DEPENDENCIES:");
        foreach (var dep in metadata.Dependencies)
        {
            Console.WriteLine($"    - {dep}");
        }
        Console.WriteLine();
    }

    return 0;
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    Console.Error.WriteLine("Use 'lcdpossible --help' for usage information.");
    return 1;
}

static Task<int> RunSensorsCommand()
{
    if (!OperatingSystem.IsWindows())
    {
        Console.Error.WriteLine("The 'sensors' command is only available on Windows.");
        Console.Error.WriteLine("It uses LibreHardwareMonitor and WMI which are Windows-specific.");
        return Task.FromResult(1);
    }

    return ListSensors.RunAsync();
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

static string? GetShowProfile(string[] args)
{
    // For "show" or "test" command, the profile can be passed as:
    // 1. lcdpossible show basic-info (second arg is the profile)
    // 2. lcdpossible test cpu-* (second arg is the profile/pattern)
    // 3. lcdpossible show -p basic-info (using -p flag)

    // Check for explicit -p or --profile flag first
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--profile" or "-p")
        {
            return args[i + 1];
        }
    }

    // Otherwise, take the argument after "show" or "test"
    for (var i = 0; i < args.Length - 1; i++)
    {
        var arg = args[i].ToLowerInvariant().TrimStart('-', '/');
        if (arg is "show" or "test")
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

static bool IsDebugMode(string[] args)
{
    return args.Any(a => a.Equals("--debug", StringComparison.OrdinalIgnoreCase) ||
                         a.Equals("-D", StringComparison.OrdinalIgnoreCase));
}

static async Task<int> ShowPanels(string[] args)
{
    var profile = GetShowProfile(args);
    var deviceIndex = GetDeviceIndex(args);
    var debug = IsDebugMode(args);

    if (debug)
    {
        Console.WriteLine("[DEBUG] Debug mode enabled");
        Console.WriteLine($"[DEBUG] Arguments: {string.Join(" ", args)}");
    }

    // If no profile specified, use the default profile panels
    if (string.IsNullOrEmpty(profile))
    {
        // Default profile: basic-info, cpu-usage-graphic, gpu-usage-graphic, ram-usage-graphic
        profile = "basic-info,cpu-usage-graphic,gpu-usage-graphic,ram-usage-graphic";
        Console.WriteLine("No panels specified, using default profile");
        if (debug)
        {
            Console.WriteLine($"[DEBUG] Default profile: {profile}");
        }
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
    if (debug)
    {
        foreach (var item in items)
        {
            Console.WriteLine($"[DEBUG] Panel: Type={item.Type}, Source={item.Source}, Duration={item.DurationSeconds}s");
        }
    }

    // Find device
    using var enumerator = new HidSharpEnumerator();
    using var deviceManager = new DeviceManager(enumerator);
    DriverRegistry.RegisterAllDrivers(deviceManager, enumerator);

    var devices = deviceManager.DiscoverDevices().ToList();
    if (debug)
    {
        Console.WriteLine($"[DEBUG] Found {devices.Count} device(s)");
        foreach (var dev in devices)
        {
            Console.WriteLine($"[DEBUG]   Device: {dev.Info.Name} ({dev.Info.VendorId:X4}:{dev.Info.ProductId:X4})");
        }
    }

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

        // Create stub system info provider - plugins provide actual hardware data
        using var systemProvider = new StubSystemInfoProvider();
        await systemProvider.InitializeAsync();
        if (debug)
        {
            Console.WriteLine($"[DEBUG] SystemProvider: {systemProvider.Name}, IsAvailable={systemProvider.IsAvailable}");
        }

        // Create plugin manager and discover plugins
        using var pluginManager = new PluginManager(debug: debug);
        pluginManager.DiscoverPlugins();
        if (debug)
        {
            var pluginInfos = pluginManager.GetDiscoveredPlugins();
            Console.WriteLine($"[DEBUG] Discovered {pluginInfos.Count} plugin(s):");
            foreach (var p in pluginInfos)
            {
                Console.WriteLine($"[DEBUG]   Plugin: {p.Id} ({p.Name}) - {p.PanelTypes.Count} panel types");
                foreach (var pt in p.PanelTypes)
                {
                    Console.WriteLine($"[DEBUG]     Panel Type: {pt}");
                }
            }
            var available = pluginManager.GetAvailablePanelTypeIds();
            Console.WriteLine($"[DEBUG] Available panel types: [{string.Join(", ", available)}]");
        }

        // Create panel factory and slideshow manager
        if (debug)
        {
            Console.WriteLine("[DEBUG] Creating PanelFactory...");
        }
        var panelFactory = new PanelFactory(pluginManager, systemProvider, debug: debug);
        if (debug)
        {
            Console.WriteLine("[DEBUG] Creating SlideshowManager...");
        }
        using var slideshow = new SlideshowManager(panelFactory, items);
        if (debug)
        {
            Console.WriteLine("[DEBUG] Initializing slideshow...");
        }
        await slideshow.InitializeAsync();
        if (debug)
        {
            Console.WriteLine("[DEBUG] Slideshow initialized");
        }

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
        var frameCount = 0;
        var lastDebugFrame = DateTime.UtcNow;

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var frame = await slideshow.RenderCurrentFrameAsync(
                    device.Capabilities.Width,
                    device.Capabilities.Height,
                    cts.Token);

                frameCount++;
                if (debug && (DateTime.UtcNow - lastDebugFrame).TotalSeconds >= 2)
                {
                    Console.WriteLine($"[DEBUG] Frame #{frameCount}: {(frame != null ? $"{frame.Width}x{frame.Height}" : "null")}");
                    lastDebugFrame = DateTime.UtcNow;
                }

                if (frame != null)
                {
                    var encoded = encoder.Encode(frame, device.Capabilities);
                    if (debug && frameCount == 1)
                    {
                        Console.WriteLine($"[DEBUG] First frame encoded: {encoded.Length} bytes");
                    }
                    await device.SendFrameAsync(encoded, ColorFormat.Jpeg, cts.Token);
                    frame.Dispose();
                }
                else if (debug && frameCount <= 3)
                {
                    Console.WriteLine($"[DEBUG] Frame #{frameCount}: null (no frame rendered)");
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

static async Task<int> RenderPanelsToFiles(string[] args)
{
    var profile = GetShowProfile(args);
    var debug = IsDebugMode(args);

    if (debug)
    {
        Console.WriteLine("[DEBUG] Debug mode enabled");
        Console.WriteLine($"[DEBUG] Arguments: {string.Join(" ", args)}");
    }

    // If no profile specified, use the default profile panels
    if (string.IsNullOrEmpty(profile))
    {
        profile = "basic-info,cpu-usage-graphic,gpu-usage-graphic,ram-usage-graphic";
        Console.WriteLine("No panels specified, using default profile");
    }

    // Determine output directory (user's home folder)
    var outputDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // Default dimensions (same as Trofeo Vision)
    const int width = 1280;
    const int height = 480;

    // Create stub system info provider - plugins provide actual hardware data
    using var systemProvider = new StubSystemInfoProvider();
    await systemProvider.InitializeAsync();

    // Create plugin manager and discover plugins
    using var pluginManager = new PluginManager(debug: debug);
    pluginManager.DiscoverPlugins();

    // Create panel factory
    var panelFactory = new PanelFactory(pluginManager, systemProvider, debug: debug);

    // Get available panels for wildcard expansion
    var availablePanels = panelFactory.AvailablePanels;
    if (debug)
    {
        Console.WriteLine($"[DEBUG] Available panels: {string.Join(", ", availablePanels)}");
    }

    // Expand wildcards in the profile
    var expandedProfile = ExpandWildcards(profile, availablePanels, debug);
    if (string.IsNullOrEmpty(expandedProfile))
    {
        Console.Error.WriteLine("Error: No panels matched the specified pattern(s)");
        return 1;
    }

    if (debug)
    {
        Console.WriteLine($"[DEBUG] Expanded profile: {expandedProfile}");
    }

    // Validate the expanded profile
    var (isValid, error) = InlineProfileParser.Validate(expandedProfile);
    if (!isValid)
    {
        Console.Error.WriteLine($"Error: {error}");
        return 1;
    }

    // Parse the profile
    var items = InlineProfileParser.Parse(expandedProfile);
    Console.WriteLine($"Rendering {items.Count} panel(s) to files...\n");

    var filesWritten = new List<string>();

    foreach (var item in items)
    {
        try
        {
            // Create the panel (Source contains the panel type ID)
            var panel = panelFactory.CreatePanel(item.Source, item.Settings);
            if (panel == null)
            {
                Console.Error.WriteLine($"Error: Could not create panel '{item.Source}'");
                return 1;
            }

            // Initialize the panel
            await panel.InitializeAsync(CancellationToken.None);

            // Render first frame
            using var frame = await panel.RenderFrameAsync(width, height, CancellationToken.None);
            if (frame == null)
            {
                Console.Error.WriteLine($"Error: Panel '{item.Source}' returned null frame");
                panel.Dispose();
                return 1;
            }

            // Generate safe filename from panel ID
            var safeFileName = GetSafeFileName(panel.PanelId);
            var outputPath = Path.Combine(outputDir, $"{safeFileName}.jpg");

            // Save as JPEG
            await frame.SaveAsJpegAsync(outputPath);
            filesWritten.Add(outputPath);
            Console.WriteLine(outputPath);

            panel.Dispose();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: Failed to render panel '{item.Source}': {ex.Message}");
            if (debug)
            {
                Console.Error.WriteLine($"[DEBUG] {ex}");
            }
            return 1;
        }
    }

    Console.WriteLine($"\nRendered {filesWritten.Count} panel(s) to files.");
    return 0;
}

static string GetSafeFileName(string panelId)
{
    // Replace invalid filename characters with underscores
    var invalidChars = Path.GetInvalidFileNameChars();
    var safeName = new string(panelId.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());

    // Also replace colons (used in parameterized panels like video:path)
    safeName = safeName.Replace(':', '_');

    return safeName;
}

static string ExpandWildcards(string profile, string[] availablePanels, bool debug)
{
    // Split profile by comma to get individual panel specs
    var parts = profile.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var expandedParts = new List<string>();

    foreach (var part in parts)
    {
        // Extract the panel type (before any | for settings)
        var pipeIndex = part.IndexOf('|');
        var panelPattern = pipeIndex >= 0 ? part[..pipeIndex] : part;
        var settings = pipeIndex >= 0 ? part[pipeIndex..] : "";

        // Check if pattern contains wildcards
        if (panelPattern.Contains('*') || panelPattern.Contains('?'))
        {
            // Convert wildcard pattern to regex
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(panelPattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            var regex = new System.Text.RegularExpressions.Regex(regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Find matching panels
            var matches = availablePanels.Where(p => regex.IsMatch(p)).OrderBy(p => p).ToList();

            if (debug)
            {
                Console.WriteLine($"[DEBUG] Pattern '{panelPattern}' matched {matches.Count} panel(s): {string.Join(", ", matches)}");
            }

            // Add each match with any settings
            foreach (var match in matches)
            {
                expandedParts.Add(match + settings);
            }
        }
        else
        {
            // No wildcard, keep as-is
            expandedParts.Add(part);
        }
    }

    return string.Join(",", expandedParts);
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

static string GetVersion()
{
    var assembly = typeof(Program).Assembly;
    var version = assembly.GetName().Version;
    return version?.ToString(3) ?? "0.0.0";
}

#region IPC Client Functions

static async Task<int> RunViaIpcAsync(string command, string[] args)
{
    Console.WriteLine($"Sending command to running service: {command}");

    try
    {
        using var client = new IpcClient();
        await client.ConnectAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        var request = BuildIpcRequest(command, args);
        var response = await client.SendAsync(request, CancellationToken.None);

        if (response.Success)
        {
            PrintIpcResponse(command, response);
            return 0;
        }
        else
        {
            Console.Error.WriteLine($"Error: {response.Error?.Message ?? "Unknown error"}");
            return 1;
        }
    }
    catch (TimeoutException)
    {
        Console.Error.WriteLine("Error: Could not connect to service (timeout)");
        Console.Error.WriteLine("The service may have stopped. Try running the command directly.");
        return 1;
    }
    catch (IpcException ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
}

static IpcRequest BuildIpcRequest(string command, string[] args)
{
    var ipcArgs = new Dictionary<string, string>();

    switch (command)
    {
        case "show":
            var panels = GetShowProfile(args);
            if (!string.IsNullOrEmpty(panels))
            {
                ipcArgs["panels"] = panels;
            }
            break;

        case "set-image":
            var imagePath = GetImagePath(args);
            if (!string.IsNullOrEmpty(imagePath))
            {
                ipcArgs["path"] = Path.GetFullPath(imagePath);
            }
            break;

        case "test-pattern":
            var testDeviceIndex = GetDeviceIndex(args);
            if (testDeviceIndex > 0)
            {
                ipcArgs["device"] = testDeviceIndex.ToString();
            }
            break;

        case "set-brightness":
            var brightnessLevel = GetBrightnessLevel(args);
            if (brightnessLevel.HasValue)
            {
                ipcArgs["level"] = brightnessLevel.Value.ToString();
            }
            var brightnessDeviceIndex = GetDeviceIndex(args);
            if (brightnessDeviceIndex > 0)
            {
                ipcArgs["device"] = brightnessDeviceIndex.ToString();
            }
            break;
    }

    return IpcRequest.Create(command, ipcArgs);
}

static void PrintIpcResponse(string command, IpcResponse response)
{
    if (response.Data is null)
    {
        Console.WriteLine("OK");
        return;
    }

    // Handle JsonElement data
    if (response.Data is JsonElement element)
    {
        switch (command)
        {
            case "status":
                PrintStatusResponse(element);
                break;

            case "list":
                PrintListResponse(element);
                break;

            default:
                // Print message if present
                if (element.TryGetProperty("message", out var messageProp))
                {
                    Console.WriteLine(messageProp.GetString());
                }
                else
                {
                    Console.WriteLine(element.ToString());
                }
                break;
        }
    }
    else
    {
        Console.WriteLine(response.Data.ToString());
    }
}

static void PrintStatusResponse(JsonElement data)
{
    Console.WriteLine("Service Status:");
    Console.WriteLine($"  Version: {data.GetProperty("version").GetString()}");
    Console.WriteLine($"  Running: {data.GetProperty("isRunning").GetBoolean()}");

    if (data.TryGetProperty("profileName", out var profileProp) && profileProp.ValueKind != JsonValueKind.Null)
    {
        Console.WriteLine($"  Profile: {profileProp.GetString()}");
    }

    Console.WriteLine($"  Devices: {data.GetProperty("connectedDevices").GetInt32()}");

    if (data.TryGetProperty("devices", out var devices) && devices.ValueKind == JsonValueKind.Array)
    {
        foreach (var device in devices.EnumerateArray())
        {
            var name = device.GetProperty("name").GetString();
            var connected = device.GetProperty("isConnected").GetBoolean();
            var width = device.GetProperty("width").GetInt32();
            var height = device.GetProperty("height").GetInt32();
            Console.WriteLine($"    - {name} ({width}x{height}) [{(connected ? "Connected" : "Disconnected")}]");
        }
    }

    if (data.TryGetProperty("currentSlideshow", out var slideshow) && slideshow.ValueKind != JsonValueKind.Null)
    {
        var current = slideshow.GetProperty("currentIndex").GetInt32() + 1;
        var total = slideshow.GetProperty("totalSlides").GetInt32();
        var panel = slideshow.GetProperty("currentPanel").GetString();
        var remaining = slideshow.GetProperty("secondsRemaining").GetInt32();
        Console.WriteLine($"  Slideshow: Slide {current}/{total} - {panel} ({remaining}s remaining)");
    }
}

static void PrintListResponse(JsonElement data)
{
    var count = data.GetProperty("count").GetInt32();
    Console.WriteLine($"Connected devices: {count}");

    if (data.TryGetProperty("devices", out var devices) && devices.ValueKind == JsonValueKind.Array)
    {
        var index = 0;
        foreach (var device in devices.EnumerateArray())
        {
            var name = device.GetProperty("name").GetString();
            var vid = device.GetProperty("vendorId").GetUInt16();
            var pid = device.GetProperty("productId").GetUInt16();
            var width = device.GetProperty("width").GetInt32();
            var height = device.GetProperty("height").GetInt32();
            var connected = device.GetProperty("isConnected").GetBoolean();

            Console.WriteLine($"  [{index}] {name}");
            Console.WriteLine($"      VID:0x{vid:X4} PID:0x{pid:X4}");
            Console.WriteLine($"      Resolution: {width}x{height}");
            Console.WriteLine($"      Status: {(connected ? "Connected" : "Disconnected")}");
            index++;
        }
    }
}

static int? GetBrightnessLevel(string[] args)
{
    // Look for brightness value after "set-brightness" or as --level/-l flag
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--level" or "-l" && int.TryParse(args[i + 1], out var level))
        {
            return level;
        }
    }

    // Also check for value directly after set-brightness command
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].ToLowerInvariant() == "set-brightness" && int.TryParse(args[i + 1], out var level))
        {
            return level;
        }
    }

    return null;
}

static int StopService()
{
    Console.Error.WriteLine("Error: Service is not running.");
    Console.Error.WriteLine("Use 'lcdpossible serve' to start the service.");
    return 1;
}

static int AdvanceSlide()
{
    Console.Error.WriteLine("Error: Service is not running.");
    Console.Error.WriteLine("Use 'lcdpossible serve' to start the service, then use 'next' to advance slides.");
    return 1;
}

static int PreviousSlide()
{
    Console.Error.WriteLine("Error: Service is not running.");
    Console.Error.WriteLine("Use 'lcdpossible serve' to start the service, then use 'previous' to go back.");
    return 1;
}

static async Task<int> SetBrightness(string[] args)
{
    var level = GetBrightnessLevel(args);
    if (!level.HasValue)
    {
        Console.Error.WriteLine("Error: Missing brightness level.");
        Console.Error.WriteLine("Usage: lcdpossible set-brightness <level>");
        Console.Error.WriteLine("       lcdpossible set-brightness --level 50");
        return 1;
    }

    if (level < 0 || level > 100)
    {
        Console.Error.WriteLine("Error: Brightness level must be between 0 and 100.");
        return 1;
    }

    // When service is not running, we need to set brightness directly
    using var enumerator = new HidSharpEnumerator();
    using var deviceManager = new DeviceManager(enumerator);

    DriverRegistry.RegisterAllDrivers(deviceManager, enumerator);

    var devices = deviceManager.DiscoverDevices().ToList();

    if (devices.Count == 0)
    {
        Console.Error.WriteLine("Error: No LCD devices found.");
        return 1;
    }

    var deviceIndex = GetDeviceIndex(args);
    if (deviceIndex < 0 || deviceIndex >= devices.Count)
    {
        Console.Error.WriteLine($"Error: Invalid device index {deviceIndex}. Available: 0-{devices.Count - 1}");
        return 1;
    }

    var device = devices[deviceIndex];

    try
    {
        await device.ConnectAsync();
        await device.SetBrightnessAsync((byte)level.Value);
        Console.WriteLine($"Brightness set to {level.Value}% on {device.Info.Name}");
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

#endregion
