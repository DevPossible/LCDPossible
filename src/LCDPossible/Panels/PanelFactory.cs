using LCDPossible.Core.Configuration;
using LCDPossible.Core.Monitoring;
using LCDPossible.Core.Rendering;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Panels;

/// <summary>
/// Factory for creating display panels by type ID.
/// </summary>
public sealed class PanelFactory
{
    private readonly ISystemInfoProvider _systemProvider;
    private readonly IProxmoxProvider? _proxmoxProvider;
    private readonly ILogger<PanelFactory>? _logger;
    private ResolvedColorScheme _colorScheme = ResolvedColorScheme.CreateDefault();

    /// <summary>
    /// List of all available panel type IDs.
    /// Animation panels use prefixes: "animated-gif:path" or "image-sequence:path".
    /// </summary>
    public static readonly string[] AvailablePanels =
    [
        "cpu-info",
        "cpu-usage-text",
        "cpu-usage-graphic",
        "ram-info",
        "ram-usage-text",
        "ram-usage-graphic",
        "gpu-info",
        "gpu-usage-text",
        "gpu-usage-graphic",
        "basic-info",
        "basic-usage-text",
        "proxmox-summary",
        "proxmox-vms",
        "animated-gif:<path>",
        "image-sequence:<folder>",
        "video:<path>",
        "html:<path>",
        "web:<url>"
    ];

    public PanelFactory(
        ISystemInfoProvider systemProvider,
        IProxmoxProvider? proxmoxProvider = null,
        ILogger<PanelFactory>? logger = null)
    {
        _systemProvider = systemProvider ?? throw new ArgumentNullException(nameof(systemProvider));
        _proxmoxProvider = proxmoxProvider;
        _logger = logger;
    }

    /// <summary>
    /// Sets the color scheme used for all created panels.
    /// </summary>
    public void SetColorScheme(ResolvedColorScheme colorScheme)
    {
        _colorScheme = colorScheme ?? ResolvedColorScheme.CreateDefault();
    }

    /// <summary>
    /// Sets the color scheme from a ColorScheme configuration.
    /// </summary>
    public void SetColorScheme(ColorScheme colorScheme)
    {
        _colorScheme = colorScheme?.Resolve() ?? ResolvedColorScheme.CreateDefault();
    }

    /// <summary>
    /// Creates a panel instance by type ID.
    /// </summary>
    /// <param name="panelTypeId">The panel type identifier. For animation panels, use format "animated-gif:path" or "image-sequence:folder".</param>
    /// <param name="settings">Optional settings dictionary for panel configuration.</param>
    /// <returns>The panel instance, or null if type is unknown.</returns>
    public IDisplayPanel? CreatePanel(string panelTypeId, Dictionary<string, string>? settings = null)
    {
        var normalizedId = panelTypeId.Trim();
        var normalizedLower = normalizedId.ToLowerInvariant();

        // Handle animation panels with path prefixes
        if (normalizedLower.StartsWith("animated-gif:"))
        {
            var path = normalizedId["animated-gif:".Length..].Trim();
            return CreateAnimatedGifPanel(path, settings);
        }

        if (normalizedLower.StartsWith("image-sequence:"))
        {
            var path = normalizedId["image-sequence:".Length..].Trim();
            return CreateImageSequencePanel(path, settings);
        }

        if (normalizedLower.StartsWith("video:"))
        {
            var path = normalizedId["video:".Length..].Trim();
            return CreateVideoPanel(path, settings);
        }

        if (normalizedLower.StartsWith("html:"))
        {
            var path = normalizedId["html:".Length..].Trim();
            return CreateHtmlPanel(path, settings);
        }

        if (normalizedLower.StartsWith("web:"))
        {
            var url = normalizedId["web:".Length..].Trim();
            return CreateWebPanel(url, settings);
        }

        // Handle standard panels
        BaseLivePanel? panel = normalizedLower switch
        {
            "cpu-info" => new CpuInfoPanel(_systemProvider),
            "cpu-usage-text" => new CpuUsageTextPanel(_systemProvider),
            "cpu-usage-graphic" => new CpuUsageGraphicPanel(_systemProvider),

            "ram-info" => new RamInfoPanel(_systemProvider),
            "ram-usage-text" => new RamUsageTextPanel(_systemProvider),
            "ram-usage-graphic" => new RamUsageGraphicPanel(_systemProvider),

            "gpu-info" => new GpuInfoPanel(_systemProvider),
            "gpu-usage-text" => new GpuUsageTextPanel(_systemProvider),
            "gpu-usage-graphic" => new GpuUsageGraphicPanel(_systemProvider),

            "basic-info" => new BasicInfoPanel(_systemProvider),
            "basic-usage-text" => new BasicUsageTextPanel(_systemProvider),

            "proxmox-summary" when _proxmoxProvider != null => new ProxmoxSummaryPanel(_proxmoxProvider),
            "proxmox-vms" when _proxmoxProvider != null => new ProxmoxVmsPanel(_proxmoxProvider),

            _ => null
        };

        if (panel != null)
        {
            panel.SetColorScheme(_colorScheme);
            return panel;
        }

        return HandleUnknownPanel(normalizedLower);
    }

    /// <summary>
    /// Creates an animated GIF panel.
    /// </summary>
    /// <param name="gifPath">Path to the animated GIF file.</param>
    /// <param name="settings">Optional settings (currently unused).</param>
    /// <returns>The panel instance, or null if path is invalid.</returns>
    public IDisplayPanel? CreateAnimatedGifPanel(string gifPathOrUrl, Dictionary<string, string>? settings = null)
    {
        if (string.IsNullOrWhiteSpace(gifPathOrUrl))
        {
            _logger?.LogWarning("Cannot create animated GIF panel: path/URL is empty");
            return null;
        }

        // Allow URLs through without file existence check
        if (!MediaHelper.IsUrl(gifPathOrUrl) && !File.Exists(gifPathOrUrl))
        {
            _logger?.LogWarning("Cannot create animated GIF panel: file not found: {Path}", gifPathOrUrl);
            return null;
        }

        try
        {
            return new AnimatedGifPanel(gifPathOrUrl);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create animated GIF panel for: {Path}", gifPathOrUrl);
            return null;
        }
    }

    /// <summary>
    /// Creates an image sequence panel.
    /// </summary>
    /// <param name="folderPath">Path to folder containing numbered image files.</param>
    /// <param name="settings">Optional settings: "fps" for frame rate (default: 30), "loop" for looping (default: true).</param>
    /// <returns>The panel instance, or null if path is invalid.</returns>
    public IDisplayPanel? CreateImageSequencePanel(string folderPath, Dictionary<string, string>? settings = null)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            _logger?.LogWarning("Cannot create image sequence panel: folder path is empty");
            return null;
        }

        if (!Directory.Exists(folderPath))
        {
            _logger?.LogWarning("Cannot create image sequence panel: folder not found: {Path}", folderPath);
            return null;
        }

        try
        {
            var fps = 30;
            var loop = true;

            if (settings != null)
            {
                if (settings.TryGetValue("fps", out var fpsStr) && int.TryParse(fpsStr, out var parsedFps))
                {
                    fps = Math.Clamp(parsedFps, 1, 120);
                }

                if (settings.TryGetValue("loop", out var loopStr))
                {
                    loop = !loopStr.Equals("false", StringComparison.OrdinalIgnoreCase);
                }
            }

            var frameRateMs = 1000 / fps;
            return new ImageSequencePanel(folderPath, frameRateMs, loop);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create image sequence panel for: {Path}", folderPath);
            return null;
        }
    }

    /// <summary>
    /// Creates a video panel.
    /// </summary>
    /// <param name="videoPath">Path to the video file.</param>
    /// <param name="settings">Optional settings: "loop" (default: true), "volume" 0-100 (default: 0).</param>
    /// <returns>The panel instance, or null if path is invalid.</returns>
    public IDisplayPanel? CreateVideoPanel(string videoPathOrUrl, Dictionary<string, string>? settings = null)
    {
        if (string.IsNullOrWhiteSpace(videoPathOrUrl))
        {
            _logger?.LogWarning("Cannot create video panel: path/URL is empty");
            return null;
        }

        // Allow URLs (including YouTube) through without file existence check
        if (!MediaHelper.IsUrl(videoPathOrUrl) && !File.Exists(videoPathOrUrl))
        {
            _logger?.LogWarning("Cannot create video panel: file not found: {Path}", videoPathOrUrl);
            return null;
        }

        try
        {
            var loop = true;
            var volume = 0f;

            if (settings != null)
            {
                if (settings.TryGetValue("loop", out var loopStr))
                {
                    loop = !loopStr.Equals("false", StringComparison.OrdinalIgnoreCase);
                }

                if (settings.TryGetValue("volume", out var volStr) && float.TryParse(volStr, out var parsedVol))
                {
                    volume = Math.Clamp(parsedVol, 0, 100);
                }
            }

            return new VideoPanel(videoPathOrUrl, loop, volume);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create video panel for: {Path}", videoPathOrUrl);
            return null;
        }
    }

    /// <summary>
    /// Creates an HTML panel that renders a local HTML file.
    /// </summary>
    /// <param name="htmlPath">Path to the HTML file.</param>
    /// <param name="settings">Optional settings: "refresh" interval in seconds (default: 5).</param>
    /// <returns>The panel instance, or null if path is invalid.</returns>
    public IDisplayPanel? CreateHtmlPanel(string htmlPath, Dictionary<string, string>? settings = null)
    {
        if (string.IsNullOrWhiteSpace(htmlPath))
        {
            _logger?.LogWarning("Cannot create HTML panel: path is empty");
            return null;
        }

        if (!File.Exists(htmlPath))
        {
            _logger?.LogWarning("Cannot create HTML panel: file not found: {Path}", htmlPath);
            return null;
        }

        try
        {
            var refreshSeconds = 5;

            if (settings != null)
            {
                if (settings.TryGetValue("refresh", out var refreshStr) && int.TryParse(refreshStr, out var parsed))
                {
                    refreshSeconds = Math.Max(1, parsed);
                }
            }

            return new HtmlPanel(htmlPath, TimeSpan.FromSeconds(refreshSeconds));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create HTML panel for: {Path}", htmlPath);
            return null;
        }
    }

    /// <summary>
    /// Creates a web panel that renders a live website.
    /// </summary>
    /// <param name="url">URL to display.</param>
    /// <param name="settings">Optional settings: "refresh" interval in seconds (default: 30), "autorefresh" true/false (default: true).</param>
    /// <returns>The panel instance, or null if URL is invalid.</returns>
    public IDisplayPanel? CreateWebPanel(string url, Dictionary<string, string>? settings = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger?.LogWarning("Cannot create web panel: URL is empty");
            return null;
        }

        try
        {
            var refreshSeconds = 30;
            var autoRefresh = true;

            if (settings != null)
            {
                if (settings.TryGetValue("refresh", out var refreshStr) && int.TryParse(refreshStr, out var parsed))
                {
                    refreshSeconds = Math.Max(1, parsed);
                }

                if (settings.TryGetValue("autorefresh", out var autoStr))
                {
                    autoRefresh = !autoStr.Equals("false", StringComparison.OrdinalIgnoreCase);
                }
            }

            return new WebPanel(url, TimeSpan.FromSeconds(refreshSeconds), autoRefresh);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create web panel for: {Url}", url);
            return null;
        }
    }

    private IDisplayPanel? HandleUnknownPanel(string panelTypeId)
    {
        _logger?.LogWarning("Unknown panel type: {PanelType}", panelTypeId);

        if (panelTypeId.StartsWith("proxmox") && _proxmoxProvider == null)
        {
            _logger?.LogWarning("Proxmox panel requested but Proxmox provider is not available");
        }

        return null;
    }
}
