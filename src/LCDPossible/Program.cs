using System.Text.Json;
using LCDPossible.Core;
using LCDPossible.Core.Configuration;
using LCDPossible.Core.Devices;
using LCDPossible.Core.Ipc;
using LCDPossible.Core.Plugins;
using LCDPossible.Core.Rendering;
using LCDPossible.Core.Transitions;
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

    // Explicit service start command (not "service" which is the management command)
    if (args.Length > 0)
    {
        var command = args[0].ToLowerInvariant().TrimStart('-', '/');
        return command is "serve" or "run";
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

        // Register profile loader with Proxmox-aware default profile factory
        builder.Services.AddSingleton<ProfileLoader>(sp =>
        {
            var logger = sp.GetService<ILogger<ProfileLoader>>();
            return new ProfileLoader(logger, () =>
            {
                // When no profile file is found, create default with Proxmox panels if configured
                if (IsProxmoxConfigured())
                {
                    return DisplayProfile.CreateDefault("proxmox-summary", "proxmox-vms");
                }
                return DisplayProfile.CreateDefault();
            });
        });

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
    var ipcCommands = new[] { "show", "set-image", "test-pattern", "status", "set-brightness", "next", "previous", "goto", "stop" };

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
        "config" => ConfigCommands.Run(args),
        "help" or "h" or "?" => ShowHelp(),
        "version" or "v" => ShowVersion(),
        "stop" => StopService(),
        "next" => AdvanceSlide(),
        "previous" => PreviousSlide(),
        "goto" => GoToSlide(args),
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
    test                    Render panels to JPEG files (no LCD required, supports wildcards)
    test-pattern            Display a test pattern on the LCD
    set-image               Send an image file to the LCD display
    show                    Quick display panels (uses default profile if no panels specified)
    profile <sub-command>   Manage display profiles (use 'profile help' for details)
    config <sub-command>    Manage configuration (use 'config help' for details)

TEST COMMAND OPTIONS:
    --resolution, -r WxH    Target resolution (default: 1280x480)
    --width W               Target width in pixels (overrides -r)
    --height H              Target height in pixels (overrides -r)
    --wait, -w <seconds>    Render frames for N seconds before capture
    --transitions, -t       Enable transitions between panels
    --output, -o <path>     Output directory (default: user home folder)

RUNTIME COMMANDS (when service is running):
    status                  Get service status and current slideshow info
    show <panels>           Change the current slideshow panels
    set-image -p <file>     Display a static image
    test-pattern            Display a test pattern
    set-brightness <0-100>  Set display brightness
    next                    Advance to next slide
    previous                Go to previous slide
    goto <index>            Jump to a specific slide (0-based, clamped to valid range)
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
    lcdpossible config set-proxmox --api-url https://proxmox:8006 --token-id user@pve!token --token-secret xxx
    lcdpossible config set-proxmox --api-url """"   Clear Proxmox API URL (disables Proxmox)
    lcdpossible config show                   Show current configuration
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
    lcdpossible test -r 800x480               Render at 800x480 resolution
    lcdpossible test -w 5 animated-gif:demo.gif   Wait 5 seconds then capture frame
    lcdpossible test -o ./output cpu-info     Save output to ./output directory
    lcdpossible test -t -w 3 a,b,c            Enable transitions, wait 3s, capture mid-transition
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

THEMES:
    LCDPossible includes several built-in color themes:

    Gamer:      cyberpunk (default), rgb-gaming
    Corporate:  executive, clean

    Set theme:          lcdpossible config set-theme <name>
    List themes:        lcdpossible config list-themes
    Per-panel override: cpu-info|@theme=executive

PANELS:
    Use 'lcdpossible list-panels' for available panel types.
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
    var isDebug = args.Any(a => a.Equals("--debug", StringComparison.OrdinalIgnoreCase) ||
                                a.Equals("-D", StringComparison.OrdinalIgnoreCase));

    // Set environment variable so all components (HtmlPanel, etc.) can detect debug mode
    if (isDebug)
    {
        Environment.SetEnvironmentVariable("LCDPOSSIBLE_DEBUG", "1");
    }

    return isDebug;
}

/// <summary>
/// Gets the resolution for test command. Checks --width, --height, and --resolution options.
/// Defaults to 1280x480 (Trofeo Vision LCD).
/// </summary>
static (int Width, int Height) GetTestResolution(string[] args)
{
    var width = 1280;
    var height = 480;

    // Check for --resolution WxH format
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals("--resolution", StringComparison.OrdinalIgnoreCase) ||
            args[i].Equals("-r", StringComparison.OrdinalIgnoreCase))
        {
            var res = args[i + 1];
            var parts = res.Split('x', 'X', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var w) &&
                int.TryParse(parts[1], out var h))
            {
                width = w;
                height = h;
            }
        }
    }

    // Check for individual --width and --height
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals("--width", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(args[i + 1], out var w))
            {
                width = w;
            }
        }
        else if (args[i].Equals("--height", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(args[i + 1], out var h))
            {
                height = h;
            }
        }
    }

    return (width, height);
}

/// <summary>
/// Gets the wait time in seconds for test command. Renders and discards frames until this time.
/// Returns 0 for no wait (capture first frame immediately).
/// </summary>
static double GetTestWaitSeconds(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals("--wait", StringComparison.OrdinalIgnoreCase) ||
            args[i].Equals("-w", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(args[i + 1], out var seconds))
            {
                return Math.Max(0, seconds);
            }
        }
    }
    return 0;
}

/// <summary>
/// Gets whether transitions are enabled for test command.
/// </summary>
static bool IsTransitionsEnabled(string[] args)
{
    // Note: -t is case-sensitive to avoid collision with -T (theme)
    return args.Any(a => a.Equals("--transitions", StringComparison.OrdinalIgnoreCase) ||
                         a.Equals("-t", StringComparison.Ordinal));
}

/// <summary>
/// Gets the themes for test command. Supports wildcards (e.g., "gam*" matches "gaming", "*" matches all).
/// Returns null if no themes specified (use default).
/// </summary>
static string[]? GetTestThemes(string[] args, bool debug)
{
    // Parse --theme argument (can be comma-separated or repeated)
    var themes = new List<string>();

    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals("--theme", StringComparison.OrdinalIgnoreCase) ||
            args[i].Equals("-T", StringComparison.OrdinalIgnoreCase))
        {
            var themeArg = args[i + 1];
            themes.AddRange(themeArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
    }

    if (themes.Count == 0)
    {
        // Use default theme from config (or fallback to cyberpunk)
        var defaultTheme = GetDefaultThemeFromConfig();
        if (debug)
        {
            Console.WriteLine($"[DEBUG] No --theme specified, using default: {defaultTheme}");
        }
        return [defaultTheme];
    }

    // Expand wildcards
    var availableThemes = ThemeManager.PresetIds.ToArray();
    var expandedThemes = new List<string>();

    foreach (var pattern in themes)
    {
        if (pattern.Contains('*') || pattern.Contains('?'))
        {
            // Convert wildcard pattern to regex
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            var regex = new System.Text.RegularExpressions.Regex(regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var matches = availableThemes.Where(t => regex.IsMatch(t)).OrderBy(t => t).ToList();

            if (debug)
            {
                Console.WriteLine($"[DEBUG] Theme pattern '{pattern}' matched {matches.Count} theme(s): {string.Join(", ", matches)}");
            }

            expandedThemes.AddRange(matches);
        }
        else if (ThemeManager.HasTheme(pattern))
        {
            expandedThemes.Add(pattern);
        }
        else
        {
            Console.Error.WriteLine($"Warning: Unknown theme '{pattern}', skipping.");
        }
    }

    return expandedThemes.Distinct().ToArray();
}

/// <summary>
/// Gets the path to the application configuration file.
/// </summary>
static string GetAppConfigPath()
{
    var configDir = PlatformPaths.GetUserDataDirectory();
    return Path.Combine(configDir, "config.json");
}

/// <summary>
/// Gets the default theme from app configuration, or "cyberpunk" if not set.
/// </summary>
static string GetDefaultThemeFromConfig()
{
    var path = GetAppConfigPath();
    if (!File.Exists(path))
    {
        return "cyberpunk";
    }

    try
    {
        var json = File.ReadAllText(path);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("General", out var general) &&
            general.TryGetProperty("DefaultTheme", out var theme))
        {
            return theme.GetString() ?? "cyberpunk";
        }
    }
    catch
    {
        // Ignore errors reading config
    }

    return "cyberpunk";
}

/// <summary>
/// Gets the output path for test command. Defaults to user's home folder.
/// </summary>
static string GetTestOutputPath(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals("--output", StringComparison.OrdinalIgnoreCase) ||
            args[i].Equals("-o", StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }
    return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}

/// <summary>
/// Gets the default profile panels, including Proxmox panels if configured.
/// </summary>
static string GetDefaultProfile(bool debug = false)
{
    // Base panels for all systems
    var panels = new List<string>
    {
        "basic-info",
        "cpu-usage-graphic",
        "gpu-usage-graphic",
        "ram-usage-graphic"
    };

    // Check if Proxmox is configured
    if (IsProxmoxConfigured(debug))
    {
        if (debug) Console.WriteLine("[DEBUG] Proxmox is configured, adding Proxmox panels to default profile");
        panels.Add("proxmox-summary");
        panels.Add("proxmox-vms");
    }

    return string.Join(",", panels);
}

/// <summary>
/// Checks if Proxmox API is configured by reading appsettings.json.
/// </summary>
static bool IsProxmoxConfigured(bool debug = false)
{
    try
    {
        // Determine config path based on platform
        string configPath;
        if (OperatingSystem.IsWindows())
        {
            configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "LCDPossible", "appsettings.json");
        }
        else if (OperatingSystem.IsMacOS())
        {
            configPath = "/Library/Application Support/LCDPossible/appsettings.json";
        }
        else
        {
            configPath = "/etc/lcdpossible/appsettings.json";
        }

        if (debug) Console.WriteLine($"[DEBUG] Checking for Proxmox config at: {configPath}");

        if (!File.Exists(configPath))
        {
            if (debug) Console.WriteLine("[DEBUG] Config file not found");
            return false;
        }

        var json = File.ReadAllText(configPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Check for LCDPossible.Proxmox section
        if (!root.TryGetProperty("LCDPossible", out var lcdSection))
        {
            if (debug) Console.WriteLine("[DEBUG] LCDPossible section not found in config");
            return false;
        }

        if (!lcdSection.TryGetProperty("Proxmox", out var proxmoxSection))
        {
            if (debug) Console.WriteLine("[DEBUG] Proxmox section not found in config");
            return false;
        }

        // Check if Proxmox is enabled and has required fields
        var enabled = proxmoxSection.TryGetProperty("Enabled", out var enabledProp) &&
                      enabledProp.ValueKind == JsonValueKind.True;

        if (!enabled)
        {
            if (debug) Console.WriteLine("[DEBUG] Proxmox not enabled in config");
            return false;
        }

        var hasApiUrl = proxmoxSection.TryGetProperty("ApiUrl", out var apiUrlProp) &&
                        apiUrlProp.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrEmpty(apiUrlProp.GetString());

        var hasTokenId = proxmoxSection.TryGetProperty("TokenId", out var tokenIdProp) &&
                         tokenIdProp.ValueKind == JsonValueKind.String &&
                         !string.IsNullOrEmpty(tokenIdProp.GetString());

        var hasTokenSecret = proxmoxSection.TryGetProperty("TokenSecret", out var tokenSecretProp) &&
                             tokenSecretProp.ValueKind == JsonValueKind.String &&
                             !string.IsNullOrEmpty(tokenSecretProp.GetString());

        var configured = hasApiUrl && hasTokenId && hasTokenSecret;
        if (debug) Console.WriteLine($"[DEBUG] Proxmox configured: {configured} (Enabled={enabled}, ApiUrl={hasApiUrl}, TokenId={hasTokenId}, TokenSecret={hasTokenSecret})");

        return configured;
    }
    catch (Exception ex)
    {
        if (debug) Console.WriteLine($"[DEBUG] Error checking Proxmox config: {ex.Message}");
        return false;
    }
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
        // Get default profile (includes Proxmox panels if configured)
        profile = GetDefaultProfile(debug);
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
            var extras = new List<string>();
            if (!string.IsNullOrEmpty(item.Theme)) extras.Add($"Theme={item.Theme}");
            if (!string.IsNullOrEmpty(item.PageEffect) && item.PageEffect != "none") extras.Add($"Effect={item.PageEffect}");
            var extraStr = extras.Count > 0 ? $", {string.Join(", ", extras)}" : "";
            Console.WriteLine($"[DEBUG] Panel: Type={item.Type}, Source={item.Source}, Duration={item.DurationSeconds}s{extraStr}");
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
        using var slideshow = new SlideshowManager(panelFactory, items, debug: debug);
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

    // Parse test command options
    var (width, height) = GetTestResolution(args);
    var waitSeconds = GetTestWaitSeconds(args);
    var transitionsEnabled = IsTransitionsEnabled(args);
    var outputDir = GetTestOutputPath(args);

    if (debug)
    {
        Console.WriteLine("[DEBUG] Debug mode enabled");
        Console.WriteLine($"[DEBUG] Arguments: {string.Join(" ", args)}");
        Console.WriteLine($"[DEBUG] Resolution: {width}x{height}");
        Console.WriteLine($"[DEBUG] Wait: {waitSeconds}s");
        Console.WriteLine($"[DEBUG] Transitions: {transitionsEnabled}");
        Console.WriteLine($"[DEBUG] Output: {outputDir}");
    }

    // If no profile specified, use the default profile panels
    if (string.IsNullOrEmpty(profile))
    {
        // Get default profile (includes Proxmox panels if configured)
        profile = GetDefaultProfile(debug);
        Console.WriteLine("No panels specified, using default profile");
        if (debug)
        {
            Console.WriteLine($"[DEBUG] Default profile: {profile}");
        }
    }

    Console.WriteLine($"Resolution: {width}x{height}");
    if (waitSeconds > 0)
    {
        Console.WriteLine($"Wait time: {waitSeconds}s (rendering frames until capture)");
    }
    if (transitionsEnabled)
    {
        Console.WriteLine("Transitions: enabled");
    }

    // Ensure output directory exists
    if (!Directory.Exists(outputDir))
    {
        try
        {
            Directory.CreateDirectory(outputDir);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: Could not create output directory '{outputDir}': {ex.Message}");
            return 1;
        }
    }

    // Create stub system info provider - plugins provide actual hardware data
    // No IPC or HID connection needed - test mode is completely standalone
    using var systemProvider = new StubSystemInfoProvider();
    await systemProvider.InitializeAsync();

    // Create plugin manager and discover plugins
    using var pluginManager = new PluginManager(debug: debug);
    pluginManager.DiscoverPlugins();

    // Create panel factory
    var panelFactory = new PanelFactory(pluginManager, systemProvider, debug: debug);

    // Get themes (if specified)
    var themes = GetTestThemes(args, debug);
    if (themes != null && themes.Length > 0)
    {
        Console.WriteLine($"Themes: {string.Join(", ", themes)}");
    }

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

    // If transitions are disabled, force TransitionType.None on all items
    if (!transitionsEnabled)
    {
        foreach (var item in items)
        {
            item.Transition = TransitionType.None;
        }
    }

    Console.WriteLine($"Rendering {items.Count} panel(s) to files...\n");

    var filesWritten = new List<string>();

    // Use slideshow manager when wait time is specified (to properly handle frame timing)
    // or when rendering multiple panels with transitions
    if (waitSeconds > 0 || (transitionsEnabled && items.Count > 1))
    {
        // Iterate over themes (or just once with no theme if none specified)
        var themesToRender = themes != null && themes.Length > 0
            ? themes
            : new string?[] { null };

        foreach (var themeId in themesToRender)
        {
            // Set theme on factory
            if (themeId != null)
            {
                var theme = ThemeManager.GetTheme(themeId);
                panelFactory.SetTheme(theme);
                if (debug)
                {
                    Console.WriteLine($"[DEBUG] Using theme: {themeId}");
                }
            }
            else
            {
                panelFactory.SetTheme(null);
            }

            // Use SlideshowManager for proper frame timing and transitions
            using var slideshow = new SlideshowManager(panelFactory, items, debug: debug);
            await slideshow.InitializeAsync(CancellationToken.None);

            if (debug)
            {
                Console.WriteLine($"[DEBUG] Slideshow initialized with {items.Count} item(s)");
            }

            // Render and discard frames until wait time elapses
            if (waitSeconds > 0)
            {
                var startTime = DateTime.UtcNow;
                var endTime = startTime.AddSeconds(waitSeconds);
                var frameInterval = TimeSpan.FromMilliseconds(1000.0 / 30); // 30 FPS
                var frameCount = 0;

                Console.Write($"Rendering frames for {waitSeconds}s: ");

                while (DateTime.UtcNow < endTime)
                {
                    using var frame = await slideshow.RenderCurrentFrameAsync(width, height, CancellationToken.None);
                    frameCount++;

                    // Progress indicator every 30 frames (~1 second)
                    if (frameCount % 30 == 0)
                    {
                        var remaining = (endTime - DateTime.UtcNow).TotalSeconds;
                        Console.Write($"\rRendering frames for {waitSeconds}s: {Math.Max(0, remaining):F1}s remaining ({frameCount} frames)    ");
                    }

                    // Maintain approximate frame rate
                    await Task.Delay(frameInterval);
                }

                Console.WriteLine($"\rRendering complete: {frameCount} frames rendered                    ");

                if (debug)
                {
                    Console.WriteLine($"[DEBUG] Rendered and discarded {frameCount} frames over {waitSeconds}s");
                }
            }

            // Capture ALL panels after wait time (not just current slide)
            // This ensures each panel's HTML/CSS has had time to initialize
            for (var i = 0; i < items.Count; i++)
            {
                // Navigate to this panel
                slideshow.GoToSlide(i);

                // Render a few frames to ensure this panel is fully rendered
                for (var warmup = 0; warmup < 5; warmup++)
                {
                    using var warmupFrame = await slideshow.RenderCurrentFrameAsync(width, height, CancellationToken.None);
                    await Task.Delay(TimeSpan.FromMilliseconds(50));
                }

                // Capture the frame
                using var capturedFrame = await slideshow.RenderCurrentFrameAsync(width, height, CancellationToken.None);
                if (capturedFrame != null)
                {
                    var currentItem = slideshow.CurrentItem;
                    var panelId = currentItem?.Source ?? $"panel-{i}";
                    var safeFileName = GetSafeFileName(panelId);
                    // Format: {panel}.jpg or {panel}-{theme}.jpg when themes are specified
                    var fileName = themeId != null
                        ? $"{safeFileName}-{themeId}.jpg"
                        : $"{safeFileName}.jpg";
                    var outputPath = Path.Combine(outputDir, fileName);

                    await capturedFrame.SaveAsJpegAsync(outputPath);
                    var fullPath = Path.GetFullPath(outputPath);
                    filesWritten.Add(fullPath);
                    if (debug)
                    {
                        var fileInfo = new FileInfo(fullPath);
                        Console.WriteLine($"[DEBUG] Written: {fullPath} ({fileInfo.Length} bytes)");

                        // Also save the HTML for HtmlPanel-based panels
                        if (slideshow.CurrentPanel is LCDPossible.Sdk.HtmlPanel htmlPanel && htmlPanel.LastRenderedHtml != null)
                        {
                            var htmlFileName = themeId != null
                                ? $"{safeFileName}-{themeId}.html"
                                : $"{safeFileName}.html";
                            var htmlPath = Path.Combine(outputDir, htmlFileName);
                            await File.WriteAllTextAsync(htmlPath, htmlPanel.LastRenderedHtml);
                            Console.WriteLine($"[DEBUG] Written: {Path.GetFullPath(htmlPath)} ({new FileInfo(htmlPath).Length} bytes)");
                        }
                    }
                    else
                    {
                        Console.WriteLine(fullPath);
                    }
                }
            }
        }
    }
    else
    {
        // Simple mode: render first frame of each panel directly
        // Themes only apply to HtmlPanel/WidgetPanel - CanvasPanel uses ColorScheme directly
        foreach (var item in items)
        {
            try
            {
                // Create the panel first to check its type
                var panel = panelFactory.CreatePanel(item.Source, item.Settings);
                if (panel == null)
                {
                    Console.Error.WriteLine($"Error: Could not create panel '{item.Source}'");
                    return 1;
                }

                // Check if panel supports themes (HtmlPanel and its descendants like WidgetPanel)
                var supportsThemes = panel is LCDPossible.Sdk.HtmlPanel;
                var safeFileName = GetSafeFileName(panel.PanelId);

                // Dispose the initial panel - we'll create fresh ones for each theme
                panel.Dispose();

                if (supportsThemes && themes != null && themes.Length > 0)
                {
                    // Render with each theme - create a new panel for each to ensure fresh initialization
                    foreach (var themeId in themes)
                    {
                        var theme = ThemeManager.GetTheme(themeId);
                        panelFactory.SetTheme(theme);

                        // Create a fresh panel with the theme applied
                        var themedPanel = panelFactory.CreatePanel(item.Source, item.Settings);
                        if (themedPanel == null)
                        {
                            Console.Error.WriteLine($"Error: Could not create panel '{item.Source}' with theme '{themeId}'");
                            return 1;
                        }

                        if (debug)
                        {
                            Console.WriteLine($"[DEBUG] Rendering {themedPanel.PanelId} with theme: {themeId}");
                        }

                        // Initialize and render
                        await themedPanel.InitializeAsync(CancellationToken.None);
                        using var frame = await themedPanel.RenderFrameAsync(width, height, CancellationToken.None);
                        if (frame == null)
                        {
                            Console.Error.WriteLine($"Error: Panel '{item.Source}' returned null frame");
                            themedPanel.Dispose();
                            return 1;
                        }

                        // Save with theme suffix
                        var fileName = $"{safeFileName}-{themeId}.jpg";
                        var outputPath = Path.Combine(outputDir, fileName);
                        await frame.SaveAsJpegAsync(outputPath);
                        var fullPath = Path.GetFullPath(outputPath);
                        filesWritten.Add(fullPath);

                        if (debug)
                        {
                            var fileInfo = new FileInfo(fullPath);
                            Console.WriteLine($"[DEBUG] Written: {fullPath} ({fileInfo.Length} bytes)");

                            // Also save the HTML for HtmlPanel-based panels
                            if (themedPanel is LCDPossible.Sdk.HtmlPanel htmlPanel && htmlPanel.LastRenderedHtml != null)
                            {
                                var htmlFileName = $"{safeFileName}-{themeId}.html";
                                var htmlPath = Path.Combine(outputDir, htmlFileName);
                                await File.WriteAllTextAsync(htmlPath, htmlPanel.LastRenderedHtml);
                                Console.WriteLine($"[DEBUG] Written: {Path.GetFullPath(htmlPath)} ({new FileInfo(htmlPath).Length} bytes)");
                            }
                        }
                        else
                        {
                            Console.WriteLine(fullPath);
                        }

                        themedPanel.Dispose();
                    }
                }
                else
                {
                    // Non-themed panel (CanvasPanel) - create fresh panel and render once with -canvas suffix
                    var canvasPanel = panelFactory.CreatePanel(item.Source, item.Settings);
                    if (canvasPanel == null)
                    {
                        Console.Error.WriteLine($"Error: Could not create panel '{item.Source}'");
                        return 1;
                    }

                    if (debug)
                    {
                        Console.WriteLine($"[DEBUG] Rendering {canvasPanel.PanelId} (CanvasPanel - themes not applicable)");
                    }

                    // Initialize and render
                    await canvasPanel.InitializeAsync(CancellationToken.None);
                    using var frame = await canvasPanel.RenderFrameAsync(width, height, CancellationToken.None);
                    if (frame == null)
                    {
                        Console.Error.WriteLine($"Error: Panel '{item.Source}' returned null frame");
                        canvasPanel.Dispose();
                        return 1;
                    }

                    // Save with -canvas suffix for CanvasPanel types
                    var fileName = $"{safeFileName}-canvas.jpg";
                    var outputPath = Path.Combine(outputDir, fileName);
                    await frame.SaveAsJpegAsync(outputPath);
                    var fullPath = Path.GetFullPath(outputPath);
                    filesWritten.Add(fullPath);

                    if (debug)
                    {
                        var fileInfo = new FileInfo(fullPath);
                        Console.WriteLine($"[DEBUG] Written: {fullPath} ({fileInfo.Length} bytes)");
                    }
                    else
                    {
                        Console.WriteLine(fullPath);
                    }

                    canvasPanel.Dispose();
                }
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

        case "goto":
            var gotoIndex = GetGotoIndex(args);
            if (gotoIndex.HasValue)
            {
                ipcArgs["index"] = gotoIndex.Value.ToString();
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

static int? GetGotoIndex(string[] args)
{
    // Look for index value after "goto" command
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].ToLowerInvariant() == "goto" && int.TryParse(args[i + 1], out var index))
        {
            return index;
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

static int GoToSlide(string[] args)
{
    // Parse index from args
    int? index = null;
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].ToLowerInvariant() == "goto" && int.TryParse(args[i + 1], out var parsed))
        {
            index = parsed;
            break;
        }
    }

    if (!index.HasValue)
    {
        Console.Error.WriteLine("Error: Missing slide index.");
        Console.Error.WriteLine("Usage: lcdpossible goto <index>");
        Console.Error.WriteLine("Example: lcdpossible goto 2");
        return 1;
    }

    Console.Error.WriteLine("Error: Service is not running.");
    Console.Error.WriteLine("Use 'lcdpossible serve' to start the service, then use 'goto' to navigate slides.");
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
