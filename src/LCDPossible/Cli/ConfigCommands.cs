using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using LCDPossible.Core;
using LCDPossible.Core.Configuration;

namespace LCDPossible.Cli;

/// <summary>
/// CLI commands for managing LCDPossible configuration.
/// </summary>
public static class ConfigCommands
{
    private const string ProxmoxPluginId = "lcdpossible.proxmox";

    public static int Run(string[] args)
    {
        // Find the subcommand after "config"
        string? subCommand = null;
        var subCommandIndex = -1;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant().TrimStart('-', '/');
            if (arg == "config" && i + 1 < args.Length)
            {
                subCommand = args[i + 1].ToLowerInvariant();
                subCommandIndex = i + 1;
                break;
            }
        }

        if (string.IsNullOrEmpty(subCommand))
        {
            return ShowHelp();
        }

        // Get remaining args after subcommand
        var subArgs = subCommandIndex + 1 < args.Length
            ? args[(subCommandIndex + 1)..]
            : Array.Empty<string>();

        return subCommand switch
        {
            "set-proxmox" => SetProxmox(subArgs),
            "validate-proxmox" => ValidateProxmox(),
            "set-theme" => SetTheme(subArgs),
            "list-themes" or "themes" => ListThemes(),
            "show" => ShowConfig(),
            "path" => ShowConfigPath(),
            "help" or "?" => ShowHelp(),
            _ => UnknownSubCommand(subCommand)
        };
    }

    private static int ShowHelp()
    {
        Console.WriteLine(@"
LCDPossible Configuration Management

USAGE:
    lcdpossible config <command> [options]

COMMANDS:
    set-theme <name>    Set the default display theme
    list-themes         List available themes
    set-proxmox         Configure Proxmox API settings
    validate-proxmox    Test Proxmox API connection
    show                Show current configuration
    path                Show configuration file path
    help                Show this help message

THEME OPTIONS:
    Available themes:
      GAMER THEMES:
        cyberpunk    - Neon cyan/magenta on deep black (default)
        rgb-gaming   - Rainbow RGB with hot pink accents

      CORPORATE THEMES:
        executive    - Professional navy blue with gold accents
        clean        - Minimal white/light theme

    Themes can also be overridden per-panel in the profile using @theme=name

SET-PROXMOX OPTIONS:
    --api-url <url>         Proxmox API URL (e.g., https://proxmox.local:8006)
    --token-id <id>         API token ID (format: user@realm!tokenname)
                            Shell-safe: use / or : instead of ! (auto-converted)
    --token-secret <secret> API token secret
    --ignore-ssl-errors     Ignore SSL certificate errors (for self-signed certs)
    --no-ignore-ssl-errors  Validate SSL certificates (default)
    --enabled               Enable Proxmox integration
    --disabled              Disable Proxmox integration

    Pass an empty string """" to clear a value.

EXAMPLES:
    # Set default theme to RGB Gaming
    lcdpossible config set-theme rgb-gaming

    # Set default theme to Executive (corporate)
    lcdpossible config set-theme executive

    # List all available themes
    lcdpossible config list-themes

    # Configure Proxmox API (use / instead of ! to avoid shell escaping)
    lcdpossible config set-proxmox --api-url https://proxmox.local:8006 \
        --token-id monitor@pve/lcdpossible --token-secret xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx

    # Test Proxmox connection
    lcdpossible config validate-proxmox

    # Show current config
    lcdpossible config show

    # Show config file location
    lcdpossible config path
");
        return 0;
    }

    private static int UnknownSubCommand(string subCommand)
    {
        Console.Error.WriteLine($"Unknown config command: {subCommand}");
        Console.Error.WriteLine("Use 'lcdpossible config help' for available commands.");
        return 1;
    }

    /// <summary>
    /// Gets the Proxmox plugin config path.
    /// This is where the Proxmox plugin reads its configuration from.
    /// </summary>
    private static string GetProxmoxConfigPath()
    {
        var pluginDataDir = PlatformPaths.GetPluginDataDirectory(ProxmoxPluginId);
        return Path.Combine(pluginDataDir, "config.json");
    }

    private static int ShowConfigPath()
    {
        var path = GetProxmoxConfigPath();
        Console.WriteLine($"Proxmox config: {path}");

        if (File.Exists(path))
        {
            Console.WriteLine("  Status: File exists");
        }
        else
        {
            Console.WriteLine("  Status: File does not exist (not configured)");
        }

        return 0;
    }

    private static int ShowConfig()
    {
        var path = GetProxmoxConfigPath();

        Console.WriteLine($"Proxmox Configuration\n");
        Console.WriteLine($"Config file: {path}");

        if (!File.Exists(path))
        {
            Console.WriteLine("\nStatus: Not configured");
            Console.WriteLine("\nUse 'lcdpossible config set-proxmox' to configure Proxmox API access.");
            return 0;
        }

        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Console.WriteLine();
            PrintJsonProperty(root, "Enabled", "");
            PrintJsonProperty(root, "ApiUrl", "");
            PrintJsonProperty(root, "TokenId", "");
            PrintJsonPropertyMasked(root, "TokenSecret", "");
            PrintJsonProperty(root, "IgnoreSslErrors", "");
            PrintJsonProperty(root, "PollingIntervalSeconds", "");
            PrintJsonProperty(root, "ShowVms", "");
            PrintJsonProperty(root, "ShowContainers", "");
            PrintJsonProperty(root, "ShowAlerts", "");

            return 0;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Error parsing configuration: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading configuration: {ex.Message}");
            return 1;
        }
    }

    private static void PrintJsonProperty(JsonElement element, string propertyName, string indent)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            var value = prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString() ?? "(empty)",
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => prop.GetRawText(),
                JsonValueKind.Null => "(null)",
                _ => prop.GetRawText()
            };

            // Show "(empty)" for empty strings
            if (prop.ValueKind == JsonValueKind.String && string.IsNullOrEmpty(prop.GetString()))
            {
                value = "(empty)";
            }

            Console.WriteLine($"{indent}{propertyName}: {value}");
        }
    }

    private static void PrintJsonPropertyMasked(JsonElement element, string propertyName, string indent)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            string value;
            if (prop.ValueKind == JsonValueKind.String)
            {
                var str = prop.GetString();
                if (string.IsNullOrEmpty(str))
                {
                    value = "(empty)";
                }
                else if (str.Length > 8)
                {
                    value = str[..4] + "..." + str[^4..];
                }
                else
                {
                    value = "****";
                }
            }
            else
            {
                value = "(not set)";
            }

            Console.WriteLine($"{indent}{propertyName}: {value}");
        }
    }

    private static int SetProxmox(string[] args)
    {
        // Parse arguments
        string? apiUrl = null;
        string? tokenId = null;
        string? tokenSecret = null;
        bool? ignoreSslErrors = null;
        bool? enabled = null;

        var hasApiUrl = false;
        var hasTokenId = false;
        var hasTokenSecret = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant();

            switch (arg)
            {
                case "--api-url":
                    if (i + 1 < args.Length)
                    {
                        apiUrl = args[++i];
                        hasApiUrl = true;
                    }
                    break;

                case "--token-id":
                    if (i + 1 < args.Length)
                    {
                        tokenId = args[++i];
                        hasTokenId = true;
                    }
                    break;

                case "--token-secret":
                    if (i + 1 < args.Length)
                    {
                        tokenSecret = args[++i];
                        hasTokenSecret = true;
                    }
                    break;

                case "--ignore-ssl-errors":
                    ignoreSslErrors = true;
                    break;

                case "--no-ignore-ssl-errors":
                    ignoreSslErrors = false;
                    break;

                case "--enabled":
                    enabled = true;
                    break;

                case "--disabled":
                    enabled = false;
                    break;
            }
        }

        // Check if any option was provided
        if (!hasApiUrl && !hasTokenId && !hasTokenSecret && !ignoreSslErrors.HasValue && !enabled.HasValue)
        {
            Console.Error.WriteLine("Error: No configuration options provided.");
            Console.Error.WriteLine("Use 'lcdpossible config help' for usage information.");
            return 1;
        }

        // Load or create config file
        var path = GetProxmoxConfigPath();
        JsonObject config;

        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                config = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                Console.WriteLine("Warning: Existing config file is invalid, creating new one.");
                config = new JsonObject();
            }
        }
        else
        {
            config = new JsonObject();
        }

        // Update values
        var changes = new List<string>();

        if (hasApiUrl)
        {
            if (string.IsNullOrEmpty(apiUrl))
            {
                config["ApiUrl"] = "";
                changes.Add("ApiUrl: (cleared)");
            }
            else
            {
                config["ApiUrl"] = apiUrl;
                changes.Add($"ApiUrl: {apiUrl}");
            }
        }

        if (hasTokenId)
        {
            if (string.IsNullOrEmpty(tokenId))
            {
                config["TokenId"] = "";
                changes.Add("TokenId: (cleared)");
            }
            else
            {
                config["TokenId"] = tokenId;
                changes.Add($"TokenId: {tokenId}");
            }
        }

        if (hasTokenSecret)
        {
            if (string.IsNullOrEmpty(tokenSecret))
            {
                config["TokenSecret"] = "";
                changes.Add("TokenSecret: (cleared)");
            }
            else
            {
                config["TokenSecret"] = tokenSecret;
                changes.Add("TokenSecret: ****");
            }
        }

        if (ignoreSslErrors.HasValue)
        {
            config["IgnoreSslErrors"] = ignoreSslErrors.Value;
            changes.Add($"IgnoreSslErrors: {ignoreSslErrors.Value}");
        }

        if (enabled.HasValue)
        {
            config["Enabled"] = enabled.Value;
            changes.Add($"Enabled: {enabled.Value}");
        }
        else
        {
            // Auto-enable if we're setting valid credentials
            var currentApiUrl = config["ApiUrl"]?.GetValue<string>() ?? "";
            var currentTokenId = config["TokenId"]?.GetValue<string>() ?? "";
            var currentTokenSecret = config["TokenSecret"]?.GetValue<string>() ?? "";

            var effectiveApiUrl = hasApiUrl ? apiUrl : currentApiUrl;
            var effectiveTokenId = hasTokenId ? tokenId : currentTokenId;
            var effectiveTokenSecret = hasTokenSecret ? tokenSecret : currentTokenSecret;

            var hasCredentials = !string.IsNullOrEmpty(effectiveApiUrl) &&
                                 !string.IsNullOrEmpty(effectiveTokenId) &&
                                 !string.IsNullOrEmpty(effectiveTokenSecret);

            if (hasCredentials && config["Enabled"] is null)
            {
                config["Enabled"] = true;
                changes.Add("Enabled: true (auto-enabled)");
            }

            // Auto-disable if all credentials are cleared
            var allCleared = string.IsNullOrEmpty(effectiveApiUrl) &&
                             string.IsNullOrEmpty(effectiveTokenId) &&
                             string.IsNullOrEmpty(effectiveTokenSecret);

            if (allCleared)
            {
                config["Enabled"] = false;
                changes.Add("Enabled: false (auto-disabled)");
            }
        }

        // Ensure directory exists
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error creating directory: {ex.Message}");
                if (!OperatingSystem.IsWindows())
                {
                    Console.Error.WriteLine("You may need to run this command with sudo.");
                }
                return 1;
            }
        }

        // Write config
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = config.ToJsonString(options);
            File.WriteAllText(path, json);

            Console.WriteLine("Proxmox configuration updated:");
            foreach (var change in changes)
            {
                Console.WriteLine($"  {change}");
            }
            Console.WriteLine($"\nConfiguration saved to: {path}");

            // Suggest validation
            Console.WriteLine("\nTo verify the connection works:");
            Console.WriteLine("  lcdpossible config validate-proxmox");

            // Remind user to restart service
            Console.WriteLine("\nNote: Restart the service for changes to take effect:");
            Console.WriteLine("  lcdpossible service restart");

            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error: Permission denied writing to {path}");
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("Run this command with sudo to write to system configuration.");
            }
            else
            {
                Console.Error.WriteLine("Run this command as Administrator to write to system configuration.");
            }
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error writing configuration: {ex.Message}");
            return 1;
        }
    }

    private static int ValidateProxmox()
    {
        Console.WriteLine("Validating Proxmox API connection...\n");

        var path = GetProxmoxConfigPath();

        if (!File.Exists(path))
        {
            Console.Error.WriteLine("Error: Proxmox not configured.");
            Console.Error.WriteLine("Use 'lcdpossible config set-proxmox' to configure first.");
            return 1;
        }

        // Load config
        string apiUrl;
        string tokenId;
        string tokenSecret;
        bool ignoreSslErrors;
        bool enabled;

        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            enabled = root.TryGetProperty("Enabled", out var enabledProp) && enabledProp.GetBoolean();
            apiUrl = root.TryGetProperty("ApiUrl", out var apiUrlProp) ? apiUrlProp.GetString() ?? "" : "";
            tokenId = root.TryGetProperty("TokenId", out var tokenIdProp) ? tokenIdProp.GetString() ?? "" : "";
            tokenSecret = root.TryGetProperty("TokenSecret", out var tokenSecretProp) ? tokenSecretProp.GetString() ?? "" : "";
            ignoreSslErrors = root.TryGetProperty("IgnoreSslErrors", out var sslProp) && sslProp.GetBoolean();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading configuration: {ex.Message}");
            return 1;
        }

        // Validate config values
        Console.WriteLine($"Config file: {path}");
        Console.WriteLine($"Enabled:     {enabled}");
        Console.WriteLine($"API URL:     {(string.IsNullOrEmpty(apiUrl) ? "(not set)" : apiUrl)}");
        Console.WriteLine($"Token ID:    {(string.IsNullOrEmpty(tokenId) ? "(not set)" : tokenId)}");
        Console.WriteLine($"Token:       {(string.IsNullOrEmpty(tokenSecret) ? "(not set)" : "****")}");
        Console.WriteLine($"Ignore SSL:  {ignoreSslErrors}");
        Console.WriteLine();

        if (!enabled)
        {
            Console.Error.WriteLine("Error: Proxmox integration is disabled.");
            Console.Error.WriteLine("Enable with: lcdpossible config set-proxmox --enabled");
            return 1;
        }

        if (string.IsNullOrEmpty(apiUrl))
        {
            Console.Error.WriteLine("Error: API URL is not configured.");
            return 1;
        }

        if (string.IsNullOrEmpty(tokenId) || string.IsNullOrEmpty(tokenSecret))
        {
            Console.Error.WriteLine("Error: Token ID or secret is not configured.");
            return 1;
        }

        // Test the connection
        Console.WriteLine("Testing connection to Proxmox API...");

        try
        {
            using var handler = new HttpClientHandler();
            if (ignoreSslErrors)
            {
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(apiUrl.TrimEnd('/') + "/api2/json/"),
                Timeout = TimeSpan.FromSeconds(10)
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "PVEAPIToken", $"{tokenId}={tokenSecret}");

            // Test with cluster status endpoint
            Console.WriteLine($"  GET {client.BaseAddress}cluster/status");

            var response = client.GetAsync("cluster/status").GetAwaiter().GetResult();

            Console.WriteLine($"  Response: {(int)response.StatusCode} {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                using var doc = JsonDocument.Parse(content);

                if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    // Find cluster name
                    string? clusterName = null;
                    int nodeCount = 0;

                    foreach (var item in data.EnumerateArray())
                    {
                        if (item.TryGetProperty("type", out var typeProp))
                        {
                            var type = typeProp.GetString();
                            if (type == "cluster" && item.TryGetProperty("name", out var nameProp))
                            {
                                clusterName = nameProp.GetString();
                            }
                            else if (type == "node")
                            {
                                nodeCount++;
                            }
                        }
                    }

                    Console.WriteLine();
                    Console.WriteLine("Connection successful!");
                    Console.WriteLine($"  Cluster: {clusterName ?? "Unknown"}");
                    Console.WriteLine($"  Nodes:   {nodeCount}");

                    return 0;
                }
            }

            // Handle error responses
            Console.WriteLine();
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Console.Error.WriteLine("Error: Authentication failed (401 Unauthorized)");
                Console.Error.WriteLine("\nPossible causes:");
                Console.Error.WriteLine("  - Invalid token ID or secret");
                Console.Error.WriteLine("  - Token has expired");
                Console.Error.WriteLine("  - API user does not have required permissions");
                Console.Error.WriteLine("\nVerify your token at: Datacenter > Permissions > API Tokens");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Console.Error.WriteLine("Error: Access denied (403 Forbidden)");
                Console.Error.WriteLine("\nThe token authenticated but lacks required permissions.");
                Console.Error.WriteLine("Grant PVEAuditor role with:");
                Console.Error.WriteLine("  pveum aclmod / -user <user>@pve -role PVEAuditor");
            }
            else
            {
                Console.Error.WriteLine($"Error: API returned {(int)response.StatusCode} {response.StatusCode}");
                var errorContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!string.IsNullOrWhiteSpace(errorContent))
                {
                    Console.Error.WriteLine($"Response: {errorContent}");
                }
            }

            return 1;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine();
            Console.Error.WriteLine($"Error: Connection failed - {ex.Message}");

            if (ex.InnerException is System.Net.Sockets.SocketException)
            {
                Console.Error.WriteLine("\nCould not connect to the Proxmox server.");
                Console.Error.WriteLine("Check that:");
                Console.Error.WriteLine("  - The API URL is correct");
                Console.Error.WriteLine("  - The server is reachable from this machine");
                Console.Error.WriteLine("  - Port 8006 is open");
            }
            else if (ex.Message.Contains("SSL", StringComparison.OrdinalIgnoreCase) ||
                     ex.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("\nSSL certificate validation failed.");
                Console.Error.WriteLine("If using a self-signed certificate, run:");
                Console.Error.WriteLine("  lcdpossible config set-proxmox --ignore-ssl-errors");
            }

            return 1;
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine();
            Console.Error.WriteLine("Error: Connection timed out");
            Console.Error.WriteLine("The server did not respond within 10 seconds.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // THEME MANAGEMENT
    // ─────────────────────────────────────────────────────────────────────────

    private static string GetAppConfigPath()
    {
        var configDir = PlatformPaths.GetUserDataDirectory();
        return Path.Combine(configDir, "config.json");
    }

    private static int ListThemes()
    {
        Console.WriteLine("Available Display Themes\n");

        var themes = ThemeManager.GetThemeList();
        var currentTheme = GetCurrentTheme();

        string? lastCategory = null;
        foreach (var (id, name, category) in themes)
        {
            if (category != lastCategory)
            {
                Console.WriteLine($"  {category.ToUpperInvariant()} THEMES:");
                lastCategory = category;
            }

            var isCurrent = id.Equals(currentTheme, StringComparison.OrdinalIgnoreCase);
            var marker = isCurrent ? " *" : "";
            var theme = ThemeManager.GetTheme(id);

            Console.WriteLine($"    {id,-14} - {name}{marker}");
        }

        Console.WriteLine();
        Console.WriteLine("  * = current default theme");
        Console.WriteLine();
        Console.WriteLine("Set theme with: lcdpossible config set-theme <name>");
        Console.WriteLine("Override per-panel with: cpu-widget|@theme=executive");

        return 0;
    }

    private static int SetTheme(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: Theme name required.");
            Console.Error.WriteLine("Usage: lcdpossible config set-theme <name>");
            Console.Error.WriteLine("Use 'lcdpossible config list-themes' to see available themes.");
            return 1;
        }

        var themeId = args[0].ToLowerInvariant();

        // Validate theme exists
        if (!ThemeManager.HasTheme(themeId))
        {
            Console.Error.WriteLine($"Error: Unknown theme '{themeId}'");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Available themes:");
            foreach (var (id, name, category) in ThemeManager.GetThemeList())
            {
                Console.Error.WriteLine($"  {id,-14} ({category})");
            }
            return 1;
        }

        // Load or create app config
        var path = GetAppConfigPath();
        JsonObject config;

        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                config = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                Console.WriteLine("Warning: Existing config file is invalid, creating new one.");
                config = new JsonObject();
            }
        }
        else
        {
            config = new JsonObject();
        }

        // Ensure General section exists
        if (!config.ContainsKey("General"))
        {
            config["General"] = new JsonObject();
        }

        // Set the theme
        var general = config["General"] as JsonObject;
        general!["DefaultTheme"] = themeId;

        // Ensure directory exists
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error creating directory: {ex.Message}");
                return 1;
            }
        }

        // Write config
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = config.ToJsonString(options);
            File.WriteAllText(path, json);

            var theme = ThemeManager.GetTheme(themeId);
            Console.WriteLine($"Default theme set to: {theme.Name} ({themeId})");
            Console.WriteLine();
            Console.WriteLine("Theme colors:");
            Console.WriteLine($"  Background: {theme.Background}");
            Console.WriteLine($"  Accent:     {theme.Accent}");
            Console.WriteLine($"  Text:       {theme.TextPrimary}");
            Console.WriteLine();
            Console.WriteLine($"Configuration saved to: {path}");
            Console.WriteLine();
            Console.WriteLine("Note: Restart the service for changes to take effect:");
            Console.WriteLine("  lcdpossible service restart");

            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error: Permission denied writing to {path}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error writing configuration: {ex.Message}");
            return 1;
        }
    }

    private static string GetCurrentTheme()
    {
        var path = GetAppConfigPath();
        if (!File.Exists(path))
        {
            return "cyberpunk";
        }

        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
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
}
