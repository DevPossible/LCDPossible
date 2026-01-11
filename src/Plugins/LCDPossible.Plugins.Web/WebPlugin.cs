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

    // Test defaults for panels that require parameters
    private static class TestDefaults
    {
        // Simple test URL for web panel
        public const string Web = "https://wttr.in/London?format=3";

        // Test HTML content
        public const string HtmlContent = """
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {
                        margin: 0;
                        padding: 40px;
                        background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
                        color: #eee;
                        font-family: 'Segoe UI', Arial, sans-serif;
                        height: 100vh;
                        box-sizing: border-box;
                        display: flex;
                        flex-direction: column;
                        justify-content: center;
                        align-items: center;
                    }
                    h1 { font-size: 48px; margin: 0 0 20px; color: #0f9d58; }
                    p { font-size: 24px; opacity: 0.8; }
                </style>
            </head>
            <body>
                <h1>HTML Panel Test</h1>
                <p>This is a test HTML panel rendered by LCDPossible.</p>
            </body>
            </html>
            """;
    }

    private string? _testHtmlPath;

    public IDisplayPanel? CreatePanel(string panelTypeId, PanelCreationContext context)
    {
        // Extract path/URL from the panel type (e.g., "html:/path/to/file.html" or "web:https://example.com")
        var path = ExtractPath(panelTypeId);
        var typePrefix = panelTypeId.ToLowerInvariant().Split(':')[0];

        // Use test defaults when no path is specified
        if (string.IsNullOrEmpty(path))
        {
            path = typePrefix switch
            {
                "web" => TestDefaults.Web,
                "html" => GetOrCreateTestHtmlFile(),
                _ => null
            };

            if (string.IsNullOrEmpty(path))
            {
                _logger?.LogWarning("Cannot create {PanelType}: no path specified and no test default available", panelTypeId);
                return null;
            }

            _logger?.LogInformation("Using test default for {PanelType}: {Path}", panelTypeId, path);
        }

        return typePrefix switch
        {
            "html" => new HtmlPanel(path),
            "web" => CreateWebPanel(path),
            _ => null
        };
    }

    private string GetOrCreateTestHtmlFile()
    {
        if (_testHtmlPath != null && File.Exists(_testHtmlPath))
        {
            return _testHtmlPath;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "LCDPossible");
        Directory.CreateDirectory(tempDir);
        _testHtmlPath = Path.Combine(tempDir, "test-panel.html");
        File.WriteAllText(_testHtmlPath, TestDefaults.HtmlContent);
        return _testHtmlPath;
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
