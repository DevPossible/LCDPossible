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

        // Load Proxmox configuration from plugin context
        _options = LoadOptions(context);

        if (!_options.Enabled)
        {
            _logger.LogInformation("Proxmox plugin loaded but disabled (Proxmox integration not configured)");
            return Task.CompletedTask;
        }

        // Create the API client
        _client = new ProxmoxApiClient(_options, _logger);

        _logger.LogInformation("Proxmox plugin initialized for {ApiUrl}", _options.ApiUrl);
        return Task.CompletedTask;
    }

    private static ProxmoxPluginOptions LoadOptions(IPluginContext context)
    {
        // Try to load from plugin data directory
        var configPath = Path.Combine(context.PluginDataDirectory, "config.json");
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var options = System.Text.Json.JsonSerializer.Deserialize<ProxmoxPluginOptions>(json);
                if (options != null)
                {
                    return options;
                }
            }
            catch
            {
                // Fall through to default
            }
        }

        // Return default (disabled) options
        return new ProxmoxPluginOptions();
    }

    public IDisplayPanel? CreatePanel(string panelTypeId, PanelCreationContext context)
    {
        if (_client == null || !_options.Enabled)
        {
            _logger?.LogWarning("Cannot create Proxmox panel: plugin not properly configured");
            return null;
        }

        return panelTypeId.ToLowerInvariant() switch
        {
            "proxmox-summary" => new ProxmoxSummaryPanel(_client),
            "proxmox-vms" => new ProxmoxVmsPanel(_client),
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
