using LCDPossible.Core.Plugins;
using LCDPossible.Core.Rendering;
using LCDPossible.Plugins.Proxmox.Api;
using LCDPossible.Plugins.Proxmox.Configuration;
using LCDPossible.Plugins.Proxmox.Panels;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Plugins.Proxmox;

/// <summary>
/// Proxmox VE plugin providing cluster monitoring panels.
/// </summary>
public sealed class ProxmoxPlugin : IPanelPlugin
{
    private ILogger? _logger;
    private ProxmoxApiClient? _client;
    private ProxmoxPluginOptions _options = new();
    private bool _debug;

    public string PluginId => "lcdpossible.proxmox";
    public string DisplayName => "LCDPossible Proxmox Panels";
    public Version Version => new(1, 0, 0);
    public string Author => "LCDPossible Team";
    public Version MinimumSdkVersion => new(1, 0, 0);

    public IReadOnlyDictionary<string, PanelTypeInfo> PanelTypes { get; } = new Dictionary<string, PanelTypeInfo>
    {
        ["proxmox-summary"] = new PanelTypeInfo
        {
            TypeId = "proxmox-summary",
            DisplayName = "Proxmox Summary",
            Description = "Proxmox cluster overview with nodes, VMs, containers, and alerts",
            Category = "Monitoring",
            IsLive = true,
            PrefixPattern = null
        },
        ["proxmox-vms"] = new PanelTypeInfo
        {
            TypeId = "proxmox-vms",
            DisplayName = "Proxmox VMs",
            Description = "List of Proxmox VMs and containers with status",
            Category = "Monitoring",
            IsLive = true,
            PrefixPattern = null
        }
    };

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _logger = context.CreateLogger("ProxmoxPlugin");

        // Check for debug mode via environment variable
        _debug = Environment.GetEnvironmentVariable("LCDPOSSIBLE_DEBUG") == "1" ||
                 Environment.GetEnvironmentVariable("PROXMOX_DEBUG") == "1";

        DebugLog($"Initializing, PluginDataDirectory={context.PluginDataDirectory}");

        // Load Proxmox configuration from plugin context
        _options = LoadOptions(context, _debug);

        DebugLog($"Options loaded - Enabled={_options.Enabled}, ApiUrl={_options.ApiUrl ?? "(null)"}, TokenId={_options.TokenId ?? "(null)"}");

        if (!_options.Enabled)
        {
            DebugLog("Plugin disabled (Proxmox integration not configured)");
            _logger.LogInformation("Proxmox plugin loaded but disabled (Proxmox integration not configured)");
            return Task.CompletedTask;
        }

        // Create the API client
        _client = new ProxmoxApiClient(_options, _logger, _debug);

        DebugLog($"Plugin initialized for {_options.ApiUrl}");
        _logger.LogInformation("Proxmox plugin initialized for {ApiUrl}", _options.ApiUrl);
        return Task.CompletedTask;
    }

    private void DebugLog(string message)
    {
        if (_debug)
        {
            Console.WriteLine($"[DEBUG] ProxmoxPlugin: {message}");
        }
    }

    private static ProxmoxPluginOptions LoadOptions(IPluginContext context, bool debug)
    {
        // Try to load from plugin data directory
        var configPath = Path.Combine(context.PluginDataDirectory, "config.json");

        if (debug) Console.WriteLine($"[DEBUG] ProxmoxPlugin: Looking for config at {configPath}");
        if (debug) Console.WriteLine($"[DEBUG] ProxmoxPlugin: Config file exists: {File.Exists(configPath)}");

        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                if (debug) Console.WriteLine($"[DEBUG] ProxmoxPlugin: Config content: {json}");

                var options = System.Text.Json.JsonSerializer.Deserialize<ProxmoxPluginOptions>(json);
                if (options != null)
                {
                    if (debug) Console.WriteLine($"[DEBUG] ProxmoxPlugin: Deserialized options - Enabled={options.Enabled}");
                    return options;
                }
            }
            catch (Exception ex)
            {
                if (debug) Console.WriteLine($"[DEBUG] ProxmoxPlugin: Failed to load config: {ex.Message}");
            }
        }

        if (debug) Console.WriteLine("[DEBUG] ProxmoxPlugin: Using default (disabled) options");
        // Return default (disabled) options
        return new ProxmoxPluginOptions();
    }

    public IDisplayPanel? CreatePanel(string panelTypeId, PanelCreationContext context)
    {
        DebugLog($"CreatePanel: panelTypeId={panelTypeId}, _client={((_client != null) ? "not null" : "null")}, Enabled={_options.Enabled}");

        // When not configured, return demo panels for testing
        if (_client == null || !_options.Enabled)
        {
            DebugLog($"Creating DEMO panel (not configured): _client={((_client != null) ? "exists" : "null")}, Enabled={_options.Enabled}");
            return panelTypeId.ToLowerInvariant() switch
            {
                "proxmox-summary" => new ProxmoxSummaryWidgetDemoPanel(),
                "proxmox-vms" => new ProxmoxVmsWidgetDemoPanel(),
                _ => null
            };
        }

        DebugLog($"Creating REAL panel for {panelTypeId}");
        return panelTypeId.ToLowerInvariant() switch
        {
            "proxmox-summary" => new ProxmoxSummaryWidgetPanel(_client),
            "proxmox-vms" => new ProxmoxVmsWidgetPanel(_client),
            _ => null
        };
    }

    public void Dispose()
    {
        _client?.Dispose();
        _client = null;
        _logger?.LogInformation("Proxmox plugin disposed");
    }
}
