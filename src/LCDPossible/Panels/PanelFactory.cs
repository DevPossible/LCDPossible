using LCDPossible.Core.Configuration;
using LCDPossible.Core.Monitoring;
using LCDPossible.Core.Plugins;
using LCDPossible.Core.Rendering;
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
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger<PanelFactory>? _logger;
    private ResolvedColorScheme _colorScheme = ResolvedColorScheme.CreateDefault();

    public PanelFactory(
        PluginManager pluginManager,
        ISystemInfoProvider? systemProvider = null,
        IProxmoxProvider? proxmoxProvider = null,
        ILoggerFactory? loggerFactory = null)
    {
        _pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
        _systemProvider = systemProvider;
        _proxmoxProvider = proxmoxProvider;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<PanelFactory>();
    }

    /// <summary>
    /// Gets a list of all available panel type IDs from discovered plugins.
    /// </summary>
    public string[] AvailablePanels => _pluginManager.GetAvailablePanelTypeIds();

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
    /// <param name="panelTypeId">The panel type identifier. For panels with paths/URLs, use format "type:argument" (e.g., "video:path/to/video.mp4").</param>
    /// <param name="settings">Optional settings dictionary for panel configuration.</param>
    /// <returns>The panel instance, or null if type is unknown or plugin fails to load.</returns>
    public async Task<IDisplayPanel?> CreatePanelAsync(string panelTypeId, Dictionary<string, string>? settings = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(panelTypeId))
        {
            _logger?.LogWarning("Cannot create panel: panel type ID is empty");
            return null;
        }

        var normalizedId = panelTypeId.Trim();

        // Parse panel type and argument (e.g., "video:path/to/file.mp4" -> type="video", arg="path/to/file.mp4")
        var (baseType, argument) = ParsePanelTypeId(normalizedId);

        // Find which plugin provides this panel type
        var pluginId = _pluginManager.FindPluginForPanelType(baseType);
        if (pluginId == null)
        {
            _logger?.LogWarning("No plugin found for panel type: {PanelType}", baseType);
            return null;
        }

        try
        {
            // Load the plugin (if not already loaded)
            var plugin = await _pluginManager.LoadPluginAsync(pluginId, cancellationToken);
            if (plugin == null)
            {
                _logger?.LogWarning("Failed to load plugin {PluginId} for panel type {PanelType}", pluginId, baseType);
                return null;
            }

            // Create panel context
            var context = new PanelCreationContext
            {
                PanelTypeId = normalizedId,
                Argument = argument,
                Settings = settings,
                SystemProvider = _systemProvider,
                ProxmoxProvider = _proxmoxProvider,
                ColorScheme = _colorScheme,
                LoggerFactory = _loggerFactory
            };

            // Create panel via plugin
            var panel = plugin.CreatePanel(normalizedId, context);

            if (panel == null)
            {
                _logger?.LogWarning("Plugin {PluginId} returned null for panel type {PanelType}", pluginId, normalizedId);
                return null;
            }

            // Apply color scheme if it's a BaseLivePanel
            if (panel is BaseLivePanel livePanel)
            {
                livePanel.SetColorScheme(_colorScheme);
            }

            return panel;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating panel {PanelType} via plugin {PluginId}", normalizedId, pluginId);
            return null;
        }
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
