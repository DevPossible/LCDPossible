using LCDPossible.Core.Plugins;
using LCDPossible.Core.Rendering;
using LCDPossible.Plugins.Web.Panels;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Plugins.Web;

/// <summary>
/// Web plugin providing HTML and web page panels.
/// </summary>
public sealed class WebPlugin : IPanelPlugin
{
    private ILogger? _logger;

    public string PluginId => "lcdpossible.web";
    public string DisplayName => "LCDPossible Web Panels";
    public Version Version => new(1, 0, 0);
    public string Author => "LCDPossible Team";
    public Version MinimumSdkVersion => new(1, 0, 0);

    public IReadOnlyDictionary<string, PanelTypeInfo> PanelTypes { get; } = new Dictionary<string, PanelTypeInfo>
    {
        ["html"] = new PanelTypeInfo
        {
            TypeId = "html",
            DisplayName = "HTML File",
            Description = "Renders a local HTML file",
            Category = "Web",
            IsLive = true,
            PrefixPattern = "html:"
        },
        ["web"] = new PanelTypeInfo
        {
            TypeId = "web",
            DisplayName = "Web Page",
            Description = "Renders a live website from URL",
            Category = "Web",
            IsLive = true,
            PrefixPattern = "web:"
        }
    };

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _logger = context.CreateLogger("WebPlugin");
        _logger.LogInformation("Web plugin initialized");
        return Task.CompletedTask;
    }

    public IDisplayPanel? CreatePanel(string panelTypeId, PanelCreationContext context)
    {
        // Extract path/URL from the panel type (e.g., "html:/path/to/file.html" or "web:https://example.com")
        var path = ExtractPath(panelTypeId);

        if (string.IsNullOrEmpty(path))
        {
            _logger?.LogWarning("Cannot create {PanelType}: no path specified", panelTypeId);
            return null;
        }

        var typePrefix = panelTypeId.ToLowerInvariant().Split(':')[0];

        return typePrefix switch
        {
            "html" => new HtmlPanel(path),
            "web" => CreateWebPanel(path),
            _ => null
        };
    }

    private static string? ExtractPath(string panelTypeId)
    {
        var colonIndex = panelTypeId.IndexOf(':');
        return colonIndex >= 0 && colonIndex < panelTypeId.Length - 1
            ? panelTypeId[(colonIndex + 1)..]
            : null;
    }

    private static WebPanel CreateWebPanel(string path)
    {
        // Path format: url or url;refresh=30;autorefresh=true
        var parts = path.Split(';');
        var url = parts[0];
        var refreshInterval = TimeSpan.FromSeconds(30);
        var autoRefresh = true;

        foreach (var part in parts.Skip(1))
        {
            if (part.StartsWith("refresh=", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(part.AsSpan(8), out var seconds))
                {
                    refreshInterval = TimeSpan.FromSeconds(seconds);
                }
            }
            else if (part.StartsWith("autorefresh=", StringComparison.OrdinalIgnoreCase))
            {
                bool.TryParse(part.AsSpan(12), out autoRefresh);
            }
        }

        return new WebPanel(url, refreshInterval, autoRefresh);
    }

    public void Dispose()
    {
        _logger?.LogInformation("Web plugin disposed");
    }
}
