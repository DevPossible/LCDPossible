using System.CommandLine;
using System.Net;
using System.Net.Sockets;
using Avalonia;
using LCDPossible.Core.Plugins;
using LCDPossible.VirtualLcd;
using LCDPossible.VirtualLcd.Protocols;

// Initialize device plugins for protocol handlers
InitializePlugins();

// Build and run CLI
var rootCommand = BuildRootCommand();
return await rootCommand.InvokeAsync(args);

static void InitializePlugins()
{
    // Create and configure the device plugin manager
    var pluginManager = new DevicePluginManager(loggerFactory: null, services: null, debug: false);
    pluginManager.DiscoverPlugins();

    // Initialize the protocol registry with plugin support
    ProtocolRegistry.InitializeDefault(pluginManager);
}

static RootCommand BuildRootCommand()
{
    var driverOption = new Option<string>(
        aliases: ["-d", "--driver"],
        description: "LCD driver/device to simulate",
        getDefaultValue: () => ProtocolRegistry.DefaultProtocolId);
    driverOption.AddCompletions(ProtocolRegistry.Default.AvailableProtocols.ToArray());

    var portOption = new Option<int?>(
        aliases: ["-p", "--port"],
        description: "UDP port to listen on (default: auto-assign from 5302)");

    var bindOption = new Option<string>(
        aliases: ["-b", "--bind"],
        description: "IP address to bind to",
        getDefaultValue: () => "0.0.0.0");

    var statsOption = new Option<bool>(
        aliases: ["--stats"],
        description: "Show statistics overlay");

    var alwaysOnTopOption = new Option<bool>(
        aliases: ["--always-on-top"],
        description: "Keep window above other windows");

    var borderlessOption = new Option<bool>(
        aliases: ["--borderless"],
        description: "Hide window decorations");

    var scaleOption = new Option<double>(
        aliases: ["--scale"],
        description: "Window scale factor",
        getDefaultValue: () => 1.0);

    var listDriversOption = new Option<bool>(
        aliases: ["--list-drivers"],
        description: "List available drivers and exit");

    var instanceNameOption = new Option<string?>(
        aliases: ["-n", "--instance-name"],
        description: "Instance name for discovery (default: auto-generated)");

    var rootCommand = new RootCommand("VirtualLCD - LCD Hardware Simulator")
    {
        driverOption,
        portOption,
        bindOption,
        statsOption,
        alwaysOnTopOption,
        borderlessOption,
        scaleOption,
        listDriversOption,
        instanceNameOption
    };

    rootCommand.SetHandler(context =>
        {
            var driver = context.ParseResult.GetValueForOption(driverOption)!;
            var portOverride = context.ParseResult.GetValueForOption(portOption);
            var bind = context.ParseResult.GetValueForOption(bindOption)!;
            var stats = context.ParseResult.GetValueForOption(statsOption);
            var alwaysOnTop = context.ParseResult.GetValueForOption(alwaysOnTopOption);
            var borderless = context.ParseResult.GetValueForOption(borderlessOption);
            var scale = context.ParseResult.GetValueForOption(scaleOption);
            var listDrivers = context.ParseResult.GetValueForOption(listDriversOption);
            var instanceName = context.ParseResult.GetValueForOption(instanceNameOption);

            if (listDrivers)
            {
                ListDrivers();
                return;
            }

            // Validate driver
            if (!ProtocolRegistry.Default.IsValidProtocol(driver))
            {
                Console.Error.WriteLine($"Unknown driver: '{driver}'");
                Console.Error.WriteLine($"Available drivers: {string.Join(", ", ProtocolRegistry.Default.AvailableProtocols)}");
                Console.Error.WriteLine("\nRun with --list-drivers to see details.");
                Environment.Exit(1);
            }

            // Parse bind address
            if (!IPAddress.TryParse(bind, out var bindAddress))
            {
                Console.Error.WriteLine($"Invalid bind address: '{bind}'");
                Environment.Exit(1);
            }

            // Auto-assign port if not specified
            int port;
            if (portOverride.HasValue)
            {
                port = portOverride.Value;
                if (port < 1 || port > 65535)
                {
                    Console.Error.WriteLine($"Invalid port: {port}");
                    Environment.Exit(1);
                }
            }
            else
            {
                port = FindAvailablePort(bindAddress);
                if (port == -1)
                {
                    Console.Error.WriteLine("Could not find an available port in range 5302-5399");
                    Environment.Exit(1);
                }
            }

            // Validate scale
            if (scale < 0.1 || scale > 10.0)
            {
                Console.Error.WriteLine($"Invalid scale: {scale}. Must be between 0.1 and 10.0");
                Environment.Exit(1);
            }

            // Set startup options
            App.Options = new StartupOptions
            {
                Protocol = driver,
                Port = port,
                BindAddress = bindAddress,
                ShowStats = stats,
                AlwaysOnTop = alwaysOnTop,
                Borderless = borderless,
                Scale = scale,
                InstanceName = instanceName
            };

            // Print startup info
            var protocolInfo = ProtocolRegistry.Default.GetProtocolInfos().First(p => p.ProtocolId.Equals(driver, StringComparison.OrdinalIgnoreCase));
            Console.WriteLine($"VirtualLCD - {protocolInfo.DisplayName}");
            Console.WriteLine($"Display: {protocolInfo.Width}x{protocolInfo.Height}");
            Console.WriteLine($"Listening on: {bind}:{port} (UDP)");
            Console.WriteLine();

            // Start Avalonia
            BuildAvaloniaApp().StartWithClassicDesktopLifetime([], Avalonia.Controls.ShutdownMode.OnMainWindowClose);
        });

    return rootCommand;
}

/// <summary>
/// Find the first available UDP port starting from 5302.
/// </summary>
static int FindAvailablePort(IPAddress bindAddress, int startPort = 5302, int endPort = 5399)
{
    for (var port = startPort; port <= endPort; port++)
    {
        if (IsPortAvailable(bindAddress, port))
        {
            return port;
        }
    }

    return -1; // No available port found
}

/// <summary>
/// Check if a UDP port is available for binding.
/// </summary>
static bool IsPortAvailable(IPAddress bindAddress, int port)
{
    try
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(bindAddress, port));
        return true;
    }
    catch (SocketException)
    {
        return false;
    }
}

static void ListDrivers()
{
    Console.WriteLine("Available LCD drivers:");
    Console.WriteLine();

    foreach (var info in ProtocolRegistry.Default.GetProtocolInfos())
    {
        var isDefault = info.ProtocolId == ProtocolRegistry.DefaultProtocolId ? " (default)" : "";
        Console.WriteLine($"  {info.ProtocolId,-20}{isDefault}");
        Console.WriteLine($"    {info.DisplayName}");
        Console.WriteLine($"    {info.Description}");
        Console.WriteLine($"    Resolution: {info.Width}x{info.Height}, HID Report: {info.HidReportSize} bytes");
        Console.WriteLine($"    VID:PID: 0x{info.VendorId:X4}:0x{info.ProductId:X4}");
        Console.WriteLine();
    }
}

static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
