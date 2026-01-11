using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LCDPossible.Cli;

/// <summary>
/// Cross-platform service management commands.
/// Supports Windows (sc.exe), Linux (systemctl), and macOS (launchctl).
/// </summary>
public static class ServiceCommands
{
    private const string ServiceName = "lcdpossible";
    private const string WindowsServiceName = "LCDPossible";

    // Linux paths
    private const string LinuxServiceFile = "/etc/systemd/system/lcdpossible.service";
    private const string LinuxInstallDir = "/opt/lcdpossible";

    // macOS paths
    private static readonly string MacOsLaunchAgentDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library/LaunchAgents");
    private const string MacOsLaunchAgent = "com.lcdpossible.service.plist";
    private static readonly string MacOsInstallDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local/share/lcdpossible");

    public static int Run(string[] args)
    {
        // Find the subcommand after "service"
        string? subCommand = null;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant().TrimStart('-', '/');
            if (arg == "service" && i + 1 < args.Length)
            {
                subCommand = args[i + 1].ToLowerInvariant();
                break;
            }
        }

        if (string.IsNullOrEmpty(subCommand))
        {
            return ShowHelp();
        }

        return subCommand switch
        {
            "install" => Install(),
            "remove" or "uninstall" => Remove(),
            "start" => Start(),
            "stop" => Stop(),
            "restart" => Restart(),
            "status" => Status(),
            "help" or "?" => ShowHelp(),
            _ => UnknownSubCommand(subCommand)
        };
    }

    private static int ShowHelp()
    {
        var platform = GetPlatformName();
        Console.WriteLine($@"
LCDPossible Service Management ({platform})

USAGE:
    lcdpossible service <command>

COMMANDS:
    install     Register the service with the system
    remove      Unregister the service from the system
    start       Start the service
    stop        Stop the service
    restart     Restart the service
    status      Show service status
    help        Show this help message

EXAMPLES:
    lcdpossible service install     Register the service
    lcdpossible service start       Start the service
    lcdpossible service status      Check if service is running
    lcdpossible service restart     Restart the service
    lcdpossible service remove      Unregister the service

NOTES:
    Windows:    Uses Windows Service Control Manager (requires admin)
    Linux:      Uses systemd (requires sudo for install/remove)
    macOS:      Uses launchd (user-level agent, no sudo required)
");
        return 0;
    }

    private static int UnknownSubCommand(string subCommand)
    {
        Console.Error.WriteLine($"Unknown service command: {subCommand}");
        Console.Error.WriteLine("Use 'lcdpossible service help' for available commands.");
        return 1;
    }

    private static string GetPlatformName()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsLinux()) return "Linux";
        if (OperatingSystem.IsMacOS()) return "macOS";
        return "Unknown";
    }

    #region Install

    private static int Install()
    {
        if (OperatingSystem.IsWindows())
            return InstallWindows();
        if (OperatingSystem.IsLinux())
            return InstallLinux();
        if (OperatingSystem.IsMacOS())
            return InstallMacOs();

        Console.Error.WriteLine("Error: Unsupported platform for service installation.");
        return 1;
    }

    private static int InstallWindows()
    {
        // Get the path to the current executable
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            Console.Error.WriteLine("Error: Could not determine executable path.");
            return 1;
        }

        Console.WriteLine($"Installing Windows service '{WindowsServiceName}'...");

        // Use sc.exe to create the service
        var (exitCode, output, error) = RunCommand("sc.exe",
            $"create {WindowsServiceName} binPath= \"\\\"{exePath}\\\" serve --service\" start= auto DisplayName= \"LCDPossible LCD Controller\"");

        if (exitCode != 0)
        {
            if (error.Contains("Access is denied", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("Error: Administrator privileges required.");
                Console.Error.WriteLine("Run this command from an elevated Command Prompt or PowerShell.");
                return 1;
            }
            if (error.Contains("service already exists", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Service is already installed.");
                return 0;
            }
            Console.Error.WriteLine($"Error: {error}");
            return exitCode;
        }

        // Set the description
        RunCommand("sc.exe", $"description {WindowsServiceName} \"Cross-platform LCD controller for HID-based displays\"");

        // Configure recovery options (restart on failure)
        RunCommand("sc.exe", $"failure {WindowsServiceName} reset= 86400 actions= restart/5000/restart/10000/restart/30000");

        Console.WriteLine("Service installed successfully.");
        Console.WriteLine($"  Service name: {WindowsServiceName}");
        Console.WriteLine($"  Executable:   {exePath}");
        Console.WriteLine();
        Console.WriteLine("Use 'lcdpossible service start' to start the service.");
        return 0;
    }

    private static int InstallLinux()
    {
        // Check if we have write access to systemd
        if (!CanWriteSystemd())
        {
            Console.Error.WriteLine("Error: Root/sudo privileges required to install systemd service.");
            Console.Error.WriteLine("Run: sudo lcdpossible service install");
            return 1;
        }

        // Find the executable path
        var exePath = FindLinuxExecutable();
        if (string.IsNullOrEmpty(exePath))
        {
            Console.Error.WriteLine("Error: Could not find LCDPossible executable.");
            Console.Error.WriteLine($"Expected at: {LinuxInstallDir}/lcdpossible");
            return 1;
        }

        Console.WriteLine($"Installing systemd service '{ServiceName}'...");

        var serviceContent = $@"[Unit]
Description=LCDPossible LCD Controller Service
After=network.target

[Service]
Type=simple
ExecStart={exePath} serve
WorkingDirectory={LinuxInstallDir}
Environment=DOTNET_ENVIRONMENT=Production
Environment=LCDPOSSIBLE_DATA_DIR=/etc/lcdpossible
Environment=LCDPOSSIBLE_CONFIG=/etc/lcdpossible/appsettings.json
Restart=on-failure
RestartSec=5

[Install]
WantedBy=multi-user.target
";

        try
        {
            File.WriteAllText(LinuxServiceFile, serviceContent);
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Error: Permission denied writing service file.");
            Console.Error.WriteLine("Run: sudo lcdpossible service install");
            return 1;
        }

        // Reload systemd and enable service
        var (exitCode, _, error) = RunCommand("systemctl", "daemon-reload");
        if (exitCode != 0)
        {
            Console.Error.WriteLine($"Error reloading systemd: {error}");
            return exitCode;
        }

        (exitCode, _, error) = RunCommand("systemctl", $"enable {ServiceName}");
        if (exitCode != 0)
        {
            Console.Error.WriteLine($"Error enabling service: {error}");
            return exitCode;
        }

        Console.WriteLine("Service installed successfully.");
        Console.WriteLine($"  Service file: {LinuxServiceFile}");
        Console.WriteLine($"  Executable:   {exePath}");
        Console.WriteLine();
        Console.WriteLine("Use 'lcdpossible service start' to start the service.");
        return 0;
    }

    private static int InstallMacOs()
    {
        var exePath = FindMacOsExecutable();
        if (string.IsNullOrEmpty(exePath))
        {
            Console.Error.WriteLine("Error: Could not find LCDPossible executable.");
            Console.Error.WriteLine($"Expected at: {MacOsInstallDir}/lcdpossible");
            return 1;
        }

        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config/lcdpossible");
        var logsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library/Logs");

        Console.WriteLine("Installing launchd agent...");

        var plistPath = Path.Combine(MacOsLaunchAgentDir, MacOsLaunchAgent);
        var plistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>com.lcdpossible.service</string>
    <key>ProgramArguments</key>
    <array>
        <string>{exePath}</string>
        <string>serve</string>
    </array>
    <key>WorkingDirectory</key>
    <string>{MacOsInstallDir}</string>
    <key>EnvironmentVariables</key>
    <dict>
        <key>DOTNET_ENVIRONMENT</key>
        <string>Production</string>
        <key>LCDPOSSIBLE_CONFIG</key>
        <string>{configDir}/appsettings.json</string>
    </dict>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>StandardOutPath</key>
    <string>{logsDir}/lcdpossible.log</string>
    <key>StandardErrorPath</key>
    <string>{logsDir}/lcdpossible.error.log</string>
</dict>
</plist>
";

        try
        {
            Directory.CreateDirectory(MacOsLaunchAgentDir);
            File.WriteAllText(plistPath, plistContent);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error writing launch agent: {ex.Message}");
            return 1;
        }

        Console.WriteLine("Launch agent installed successfully.");
        Console.WriteLine($"  Agent file: {plistPath}");
        Console.WriteLine($"  Executable: {exePath}");
        Console.WriteLine();
        Console.WriteLine("Use 'lcdpossible service start' to start the service.");
        return 0;
    }

    #endregion

    #region Remove

    private static int Remove()
    {
        if (OperatingSystem.IsWindows())
            return RemoveWindows();
        if (OperatingSystem.IsLinux())
            return RemoveLinux();
        if (OperatingSystem.IsMacOS())
            return RemoveMacOs();

        Console.Error.WriteLine("Error: Unsupported platform.");
        return 1;
    }

    private static int RemoveWindows()
    {
        Console.WriteLine($"Removing Windows service '{WindowsServiceName}'...");

        // Stop the service first
        RunCommand("sc.exe", $"stop {WindowsServiceName}");

        // Delete the service
        var (exitCode, _, error) = RunCommand("sc.exe", $"delete {WindowsServiceName}");

        if (exitCode != 0)
        {
            if (error.Contains("Access is denied", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("Error: Administrator privileges required.");
                return 1;
            }
            if (error.Contains("service does not exist", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Service is not installed.");
                return 0;
            }
            Console.Error.WriteLine($"Error: {error}");
            return exitCode;
        }

        Console.WriteLine("Service removed successfully.");
        return 0;
    }

    private static int RemoveLinux()
    {
        Console.WriteLine($"Removing systemd service '{ServiceName}'...");

        // Stop and disable the service
        RunCommand("systemctl", $"stop {ServiceName}");
        RunCommand("systemctl", $"disable {ServiceName}");

        // Remove the service file
        if (File.Exists(LinuxServiceFile))
        {
            try
            {
                File.Delete(LinuxServiceFile);
            }
            catch (UnauthorizedAccessException)
            {
                Console.Error.WriteLine("Error: Permission denied. Run with sudo.");
                return 1;
            }
        }

        // Reload systemd
        RunCommand("systemctl", "daemon-reload");

        Console.WriteLine("Service removed successfully.");
        return 0;
    }

    private static int RemoveMacOs()
    {
        var plistPath = Path.Combine(MacOsLaunchAgentDir, MacOsLaunchAgent);

        Console.WriteLine("Removing launchd agent...");

        // Unload first
        RunCommand("launchctl", $"unload \"{plistPath}\"");

        // Remove the plist file
        if (File.Exists(plistPath))
        {
            try
            {
                File.Delete(plistPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error removing agent file: {ex.Message}");
                return 1;
            }
        }

        Console.WriteLine("Launch agent removed successfully.");
        return 0;
    }

    #endregion

    #region Start/Stop/Restart

    private static int Start()
    {
        Console.WriteLine("Starting service...");

        if (OperatingSystem.IsWindows())
        {
            var (exitCode, _, error) = RunCommand("sc.exe", $"start {WindowsServiceName}");
            if (exitCode != 0)
            {
                if (error.Contains("Access is denied", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine("Error: Administrator privileges required.");
                    return 1;
                }
                Console.Error.WriteLine($"Error: {error}");
                return exitCode;
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            var (exitCode, _, error) = RunCommand("systemctl", $"start {ServiceName}");
            if (exitCode != 0)
            {
                Console.Error.WriteLine($"Error: {error}");
                return exitCode;
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            var plistPath = Path.Combine(MacOsLaunchAgentDir, MacOsLaunchAgent);
            var (exitCode, _, error) = RunCommand("launchctl", $"load \"{plistPath}\"");
            if (exitCode != 0)
            {
                Console.Error.WriteLine($"Error: {error}");
                return exitCode;
            }
        }
        else
        {
            Console.Error.WriteLine("Error: Unsupported platform.");
            return 1;
        }

        Console.WriteLine("Service started.");
        return 0;
    }

    private static int Stop()
    {
        Console.WriteLine("Stopping service...");

        if (OperatingSystem.IsWindows())
        {
            var (exitCode, _, error) = RunCommand("sc.exe", $"stop {WindowsServiceName}");
            if (exitCode != 0 && !error.Contains("not been started", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"Error: {error}");
                return exitCode;
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            var (exitCode, _, error) = RunCommand("systemctl", $"stop {ServiceName}");
            if (exitCode != 0)
            {
                Console.Error.WriteLine($"Error: {error}");
                return exitCode;
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            var plistPath = Path.Combine(MacOsLaunchAgentDir, MacOsLaunchAgent);
            var (exitCode, _, error) = RunCommand("launchctl", $"unload \"{plistPath}\"");
            if (exitCode != 0)
            {
                Console.Error.WriteLine($"Error: {error}");
                return exitCode;
            }
        }
        else
        {
            Console.Error.WriteLine("Error: Unsupported platform.");
            return 1;
        }

        Console.WriteLine("Service stopped.");
        return 0;
    }

    private static int Restart()
    {
        Console.WriteLine("Restarting service...");

        if (OperatingSystem.IsWindows())
        {
            RunCommand("sc.exe", $"stop {WindowsServiceName}");
            Thread.Sleep(1000); // Wait for stop
            var (exitCode, _, error) = RunCommand("sc.exe", $"start {WindowsServiceName}");
            if (exitCode != 0)
            {
                Console.Error.WriteLine($"Error: {error}");
                return exitCode;
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            var (exitCode, _, error) = RunCommand("systemctl", $"restart {ServiceName}");
            if (exitCode != 0)
            {
                Console.Error.WriteLine($"Error: {error}");
                return exitCode;
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            var plistPath = Path.Combine(MacOsLaunchAgentDir, MacOsLaunchAgent);
            RunCommand("launchctl", $"unload \"{plistPath}\"");
            Thread.Sleep(500);
            var (exitCode, _, error) = RunCommand("launchctl", $"load \"{plistPath}\"");
            if (exitCode != 0)
            {
                Console.Error.WriteLine($"Error: {error}");
                return exitCode;
            }
        }
        else
        {
            Console.Error.WriteLine("Error: Unsupported platform.");
            return 1;
        }

        Console.WriteLine("Service restarted.");
        return 0;
    }

    #endregion

    #region Status

    private static int Status()
    {
        if (OperatingSystem.IsWindows())
            return StatusWindows();
        if (OperatingSystem.IsLinux())
            return StatusLinux();
        if (OperatingSystem.IsMacOS())
            return StatusMacOs();

        Console.Error.WriteLine("Error: Unsupported platform.");
        return 1;
    }

    private static int StatusWindows()
    {
        var (exitCode, output, _) = RunCommand("sc.exe", $"query {WindowsServiceName}");

        if (exitCode != 0 || output.Contains("FAILED 1060"))
        {
            Console.WriteLine("Service: Not installed");
            return 0;
        }

        var isRunning = output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
        var isStopped = output.Contains("STOPPED", StringComparison.OrdinalIgnoreCase);

        Console.WriteLine($"Service: {WindowsServiceName}");
        Console.WriteLine($"Status:  {(isRunning ? "Running" : isStopped ? "Stopped" : "Unknown")}");

        return 0;
    }

    private static int StatusLinux()
    {
        if (!File.Exists(LinuxServiceFile))
        {
            Console.WriteLine("Service: Not installed");
            return 0;
        }

        var (_, output, _) = RunCommand("systemctl", $"is-active {ServiceName}");
        var isActive = output.Trim() == "active";

        var (_, enabledOutput, _) = RunCommand("systemctl", $"is-enabled {ServiceName}");
        var isEnabled = enabledOutput.Trim() == "enabled";

        Console.WriteLine($"Service: {ServiceName}");
        Console.WriteLine($"Status:  {(isActive ? "Running" : "Stopped")}");
        Console.WriteLine($"Enabled: {(isEnabled ? "Yes" : "No")}");

        return 0;
    }

    private static int StatusMacOs()
    {
        var plistPath = Path.Combine(MacOsLaunchAgentDir, MacOsLaunchAgent);

        if (!File.Exists(plistPath))
        {
            Console.WriteLine("Service: Not installed");
            return 0;
        }

        var (_, output, _) = RunCommand("launchctl", "list");
        var isLoaded = output.Contains("com.lcdpossible.service");

        Console.WriteLine($"Service: com.lcdpossible.service");
        Console.WriteLine($"Status:  {(isLoaded ? "Loaded" : "Not loaded")}");
        Console.WriteLine($"Agent:   {plistPath}");

        return 0;
    }

    #endregion

    #region Helpers

    private static (int ExitCode, string Output, string Error) RunCommand(string command, string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return (-1, "", "Failed to start process");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, output, error);
        }
        catch (Exception ex)
        {
            return (-1, "", ex.Message);
        }
    }

    private static bool CanWriteSystemd()
    {
        try
        {
            var testFile = Path.Combine("/etc/systemd/system", ".lcdpossible-test");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindLinuxExecutable()
    {
        var expected = Path.Combine(LinuxInstallDir, "lcdpossible");
        if (File.Exists(expected))
            return expected;

        // Also check the current executable path
        var current = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(current) && File.Exists(current))
            return current;

        return null;
    }

    private static string? FindMacOsExecutable()
    {
        var expected = Path.Combine(MacOsInstallDir, "lcdpossible");
        if (File.Exists(expected))
            return expected;

        // Also check the current executable path
        var current = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(current) && File.Exists(current))
            return current;

        return null;
    }

    #endregion
}
