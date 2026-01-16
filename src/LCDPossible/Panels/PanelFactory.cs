using LCDPossible.Core.Configuration;
using LCDPossible.Core.Monitoring;
using LCDPossible.Core.Plugins;
using LCDPossible.Core.Rendering;
using LCDPossible.Core.Services;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Panels;

/// <summary>
/// Factory for creating display panels by type ID.
/// Delegates panel creation to the appropriate plugin via PluginManager.
/// </summary>
public sealed class PanelFactory
{
    private readonly PluginManager _pluginManager;
    private readonly ISystemInfoProvider? _systemProvider;
    private readonly IProxmoxProvider? _proxmoxProvider;
    private readonly ILcdServices? _services;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger<PanelFactory>? _logger;
    private readonly bool _debug;
    private ResolvedColorScheme _colorScheme = ResolvedColorScheme.CreateDefault();
    private Theme? _currentTheme;

    public PanelFactory(
        PluginManager pluginManager,
        ISystemInfoProvider? systemProvider = null,
        IProxmoxProvider? proxmoxProvider = null,
        ILcdServices? services = null,
        ILoggerFactory? loggerFactory = null,
        bool debug = false)
    {
        _pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
        _systemProvider = systemProvider;
        _proxmoxProvider = proxmoxProvider;
        _services = services;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<PanelFactory>();
        _debug = debug;
    }

    /// <summary>
    /// Gets a list of all available panel type IDs from discovered plugins.
    /// </summary>
    public string[] AvailablePanels => _pluginManager.GetAvailablePanelTypeIds();

    /// <summary>
    /// Gets all available panel metadata grouped by category.
    /// </summary>
    /// <returns>Dictionary of category name to list of panel metadata.</returns>
    public Dictionary<string, List<PluginPanelTypeMetadata>> GetAllPanelMetadataByCategory()
    {
        var result = new Dictionary<string, List<PluginPanelTypeMetadata>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, panelType) in _pluginManager.GetAvailablePanelTypes())
        {
            var category = panelType.Category ?? "Other";
            if (!result.ContainsKey(category))
            {
                result[category] = [];
            }
            result[category].Add(panelType);
        }

        // Sort panels within each category by display name
        foreach (var panels in result.Values)
        {
            panels.Sort((a, b) =>
                string.Compare(a.DisplayName ?? a.DisplayId, b.DisplayName ?? b.DisplayId, StringComparison.OrdinalIgnoreCase));
        }

        return result;
    }

    /// <summary>
    /// Gets all available panel metadata as a flat list.
    /// </summary>
    public IEnumerable<PluginPanelTypeMetadata> GetAllPanelMetadata()
    {
        return _pluginManager.GetAvailablePanelTypes().Select(t => t.PanelType);
    }

    /// <summary>
    /// Gets metadata for a specific panel type.
    /// </summary>
    /// <param name="panelTypeId">The panel type ID or prefix pattern (e.g., "cpu-info" or "video:").</param>
    /// <returns>Panel metadata if found, null otherwise.</returns>
    public PluginPanelTypeMetadata? GetPanelMetadata(string panelTypeId)
    {
        if (string.IsNullOrWhiteSpace(panelTypeId))
            return null;

        var normalizedId = panelTypeId.Trim().ToLowerInvariant();

        // First try exact match
        foreach (var (_, panelType) in _pluginManager.GetAvailablePanelTypes())
        {
            if (panelType.TypeId.Equals(normalizedId, StringComparison.OrdinalIgnoreCase))
                return panelType;

            // Check prefix pattern (with or without colon)
            if (panelType.PrefixPattern != null)
            {
                var prefixWithoutColon = panelType.PrefixPattern.TrimEnd(':');
                if (prefixWithoutColon.Equals(normalizedId, StringComparison.OrdinalIgnoreCase) ||
                    panelType.PrefixPattern.Equals(normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    return panelType;
                }
            }
        }

        // Try matching by base type (for "video:path" -> look up "video")
        var colonIndex = normalizedId.IndexOf(':');
        if (colonIndex > 0)
        {
            var baseType = normalizedId[..colonIndex];
            return GetPanelMetadata(baseType);
        }

        return null;
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
    /// Sets the theme for all created panels.
    /// This also updates the color scheme from the theme.
    /// </summary>
    public void SetTheme(Theme? theme)
    {
        _currentTheme = theme;
        if (theme != null)
        {
            _colorScheme = theme.ToColorScheme().Resolve();
        }
    }

    /// <summary>
    /// Result of attempting to create a panel.
    /// </summary>
    public record PanelCreationResult(IDisplayPanel? Panel, string? ErrorMessage);

    /// <summary>
    /// Creates a panel instance by type ID, returning both the panel and any error message.
    /// </summary>
    /// <param name="panelTypeId">The panel type identifier.</param>
    /// <param name="settings">Optional settings dictionary for panel configuration.</param>
    /// <returns>Result containing the panel (if successful) or error message (if failed).</returns>
    public async Task<PanelCreationResult> TryCreatePanelAsync(string panelTypeId, Dictionary<string, string>? settings = null, CancellationToken cancellationToken = default)
    {
        if (_debug) Console.WriteLine($"[DEBUG] PanelFactory.TryCreatePanelAsync: Creating panel '{panelTypeId}'");

        if (string.IsNullOrWhiteSpace(panelTypeId))
        {
            return new PanelCreationResult(null, "Panel type ID is empty");
        }

        var normalizedId = panelTypeId.Trim();
        var (baseType, argument) = ParsePanelTypeId(normalizedId);

        // Find which plugin provides this panel type
        var pluginId = _pluginManager.FindPluginForPanelType(baseType);
        if (pluginId == null)
        {
            var error = $"No plugin registered for type '{baseType}'";
            if (_debug) Console.WriteLine($"[DEBUG] PanelFactory: {error}");
            return new PanelCreationResult(null, error);
        }

        try
        {
            // Load the plugin (if not already loaded)
            var plugin = await _pluginManager.LoadPluginAsync(pluginId, cancellationToken);
            if (plugin == null)
            {
                var error = $"Failed to load plugin '{pluginId}'";
                if (_debug) Console.WriteLine($"[DEBUG] PanelFactory: {error}");
                return new PanelCreationResult(null, error);
            }

            // Create panel context with theme available at creation time (H004 fix)
            var context = new PanelCreationContext
            {
                PanelTypeId = normalizedId,
                Argument = argument,
                Settings = settings,
                Services = _services,
#pragma warning disable CS0618 // Type or member is obsolete
                SystemProvider = _systemProvider,
                ProxmoxProvider = _proxmoxProvider,
#pragma warning restore CS0618
                ColorScheme = _colorScheme,
                Theme = _currentTheme,
                LoggerFactory = _loggerFactory
            };

            // Create panel via plugin
            var panel = plugin.CreatePanel(normalizedId, context);

            if (panel == null)
            {
                var error = $"Plugin '{pluginId}' returned null for '{normalizedId}'";
                if (_debug) Console.WriteLine($"[DEBUG] PanelFactory: {error}");
                return new PanelCreationResult(null, error);
            }

            // Apply theme to HtmlPanel-based panels (new widget system)
            if (panel is LCDPossible.Sdk.HtmlPanel htmlPanel)
            {
                htmlPanel.SetTheme(_currentTheme);
            }
            else if (panel is LCDPossible.Sdk.BasePanel basePanel)
            {
                basePanel.SetColorScheme(_colorScheme);
            }

            return new PanelCreationResult(panel, null);
        }
        catch (Exception ex)
        {
            var error = $"{ex.GetType().Name}: {ex.Message}";
            if (_debug) Console.WriteLine($"[DEBUG] PanelFactory: Exception loading panel '{normalizedId}' via plugin '{pluginId}': {error}");
            _logger?.LogError(ex, "Error creating panel {PanelType} via plugin {PluginId}", normalizedId, pluginId);
            return new PanelCreationResult(null, error);
        }
    }

    /// <summary>
    /// Creates a panel instance by type ID.
    /// </summary>
    /// <param name="panelTypeId">The panel type identifier. For panels with paths/URLs, use format "type:argument" (e.g., "video:path/to/video.mp4").</param>
    /// <param name="settings">Optional settings dictionary for panel configuration.</param>
    /// <returns>The panel instance, or null if type is unknown or plugin fails to load.</returns>
    public async Task<IDisplayPanel?> CreatePanelAsync(string panelTypeId, Dictionary<string, string>? settings = null, CancellationToken cancellationToken = default)
    {
        var result = await TryCreatePanelAsync(panelTypeId, settings, cancellationToken);
        if (result.ErrorMessage != null)
        {
            _logger?.LogWarning("Failed to create panel '{PanelType}': {Error}", panelTypeId, result.ErrorMessage);
        }
        return result.Panel;
    }

    /// <summary>
    /// Synchronous wrapper for TryCreatePanelAsync.
    /// </summary>
    public PanelCreationResult TryCreatePanel(string panelTypeId, Dictionary<string, string>? settings = null)
    {
        return TryCreatePanelAsync(panelTypeId, settings).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Synchronous wrapper for CreatePanelAsync for backward compatibility.
    /// </summary>
    public IDisplayPanel? CreatePanel(string panelTypeId, Dictionary<string, string>? settings = null)
    {
        return CreatePanelAsync(panelTypeId, settings).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Parses a panel type ID into its base type and optional argument.
    /// </summary>
    /// <example>
    /// "cpu-info" -> ("cpu-info", null)
    /// "video:path/to/file.mp4" -> ("video", "path/to/file.mp4")
    /// "animated-gif:https://example.com/image.gif" -> ("animated-gif", "https://example.com/image.gif")
    /// </example>
    private static (string baseType, string? argument) ParsePanelTypeId(string panelTypeId)
    {
        var colonIndex = panelTypeId.IndexOf(':');
        if (colonIndex <= 0)
        {
            return (panelTypeId.ToLowerInvariant(), null);
        }

        var baseType = panelTypeId[..colonIndex].ToLowerInvariant();
        var argument = panelTypeId[(colonIndex + 1)..].Trim();

        return (baseType, string.IsNullOrEmpty(argument) ? null : argument);
    }
}
