using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LCDPossible.Core.Plugins;

/// <summary>
/// Manages plugin discovery, loading, and lifecycle.
/// Plugins are discovered from both built-in and user directories,
/// but only loaded on demand when a panel type is requested.
/// </summary>
public sealed class PluginManager : IDisposable
{
    private readonly ILogger<PluginManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider? _services;
    private readonly bool _debug;

    private readonly Dictionary<string, PluginEntry> _availablePlugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PluginEntry> _loadedPlugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _panelTypeToPlugin = new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;
    private static int _sharedAssembliesPreloaded; // 0 = false, 1 = true (using int for Interlocked)

    /// <summary>
    /// Creates a new plugin manager.
    /// </summary>
    public PluginManager(ILoggerFactory? loggerFactory = null, IServiceProvider? services = null, bool debug = false)
    {
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<PluginManager>();
        _services = services;
        _debug = debug;
    }

    /// <summary>
    /// Gets metadata for all discovered plugins.
    /// </summary>
    public IReadOnlyDictionary<string, PluginMetadata> DiscoveredPlugins =>
        _availablePlugins.ToDictionary(p => p.Key, p => p.Value.Metadata);

    /// <summary>
    /// Gets IDs of currently loaded plugins.
    /// </summary>
    public IReadOnlyCollection<string> LoadedPluginIds => _loadedPlugins.Keys;

    /// <summary>
    /// Ensures shared assemblies (SDK, Core, etc.) are loaded in the default context
    /// before plugins try to use them. This is essential for single-file deployments.
    /// </summary>
    private void EnsureSharedAssembliesLoaded()
    {
        // Thread-safe check using Interlocked
        if (Interlocked.CompareExchange(ref _sharedAssembliesPreloaded, 1, 0) == 1)
            return; // Already loaded by another thread

        // List of assemblies that must be available to plugins
        var sharedAssemblies = new[]
        {
            "LCDPossible.Sdk",
            "LCDPossible.Core",
            "SixLabors.ImageSharp",
            "SixLabors.ImageSharp.Drawing",
            "SixLabors.Fonts"
        };

        foreach (var assemblyName in sharedAssemblies)
        {
            try
            {
                // Check if already loaded
                var loaded = AssemblyLoadContext.Default.Assemblies
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));

                if (loaded != null)
                {
                    _logger.LogDebug("Shared assembly '{AssemblyName}' already loaded", assemblyName);
                    continue;
                }

                // Try to load from disk first (non-single-file deployment)
                var assemblyPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.dll");
                if (File.Exists(assemblyPath))
                {
                    AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
                    _logger.LogDebug("Loaded shared assembly '{AssemblyName}' from disk", assemblyName);
                    continue;
                }

                // Try to load from the bundle (single-file deployment)
                try
                {
                    AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(assemblyName));
                    _logger.LogDebug("Loaded shared assembly '{AssemblyName}' from bundle", assemblyName);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Could not preload '{AssemblyName}': {Message}", assemblyName, ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error preloading '{AssemblyName}': {Message}", assemblyName, ex.Message);
            }
        }
    }

    /// <summary>
    /// Discovers all available plugins from built-in and user directories.
    /// Does not load the plugins - just reads their manifests.
    /// </summary>
    public void DiscoverPlugins()
    {
        _logger.LogInformation("Discovering plugins...");
        _logger.LogDebug("Starting plugin discovery");

        // Built-in plugins (lower priority)
        var builtInDir = PlatformPaths.GetBuiltInPluginsDirectory();
        _logger.LogDebug("Built-in plugins directory: {Directory}", builtInDir);
        if (Directory.Exists(builtInDir))
        {
            ScanPluginDirectory(builtInDir, isBuiltIn: true);
        }
        else
        {
            _logger.LogDebug("Built-in directory does not exist: {Directory}", builtInDir);
        }

        // User plugins (higher priority - can override built-in)
        var userDir = PlatformPaths.GetUserPluginsDirectory();
        _logger.LogDebug("User plugins directory: {Directory}", userDir);
        if (Directory.Exists(userDir))
        {
            ScanPluginDirectory(userDir, isBuiltIn: false);
        }
        else
        {
            _logger.LogDebug("User directory does not exist (OK - optional): {Directory}", userDir);
        }

        _logger.LogInformation("Discovered {Count} plugins with {PanelCount} panel types",
            _availablePlugins.Count, _panelTypeToPlugin.Count);
    }

    private void ScanPluginDirectory(string directory, bool isBuiltIn)
    {
        var dirs = Directory.GetDirectories(directory);
        _logger.LogDebug("Scanning {Directory}, found {Count} subdirectories", directory, dirs.Length);

        foreach (var pluginDir in dirs)
        {
            _logger.LogDebug("Checking plugin directory: {Directory}", Path.GetFileName(pluginDir));

            var manifestPath = Path.Combine(pluginDir, "plugin.json");
            if (!File.Exists(manifestPath))
            {
                _logger.LogDebug("No plugin.json found in {Directory}", pluginDir);
                continue;
            }

            try
            {
                var metadata = PluginMetadata.LoadFromFile(manifestPath);
                if (metadata == null || string.IsNullOrEmpty(metadata.Id))
                {
                    _logger.LogWarning("Invalid plugin manifest: {Path}", manifestPath);
                    continue;
                }

                var assemblyPath = Path.Combine(pluginDir, metadata.AssemblyName);
                if (!File.Exists(assemblyPath))
                {
                    _logger.LogWarning("Plugin assembly not found: {Path}", assemblyPath);
                    continue;
                }

                // Check SDK version compatibility
                var minSdk = metadata.GetMinimumSdkVersion();
                if (!SdkVersion.IsCompatible(minSdk))
                {
                    _logger.LogWarning("Plugin {PluginId} requires SDK {Required}, but host has {Current}",
                        metadata.Id, minSdk, SdkVersion.Current);
                    continue;
                }

                var entry = new PluginEntry
                {
                    PluginId = metadata.Id,
                    PluginDirectory = pluginDir,
                    AssemblyPath = assemblyPath,
                    Metadata = metadata,
                    IsBuiltIn = isBuiltIn
                };

                // Check for plugin ID collision (H008 fix)
                if (_availablePlugins.TryGetValue(metadata.Id, out var existingEntry))
                {
                    _logger.LogWarning(
                        "Plugin '{PluginId}' already registered from '{ExistingPath}', " +
                        "overwriting with plugin from '{NewPath}'",
                        metadata.Id, existingEntry.PluginDirectory, pluginDir);
                }

                // User plugins override built-in plugins with same ID
                _availablePlugins[metadata.Id] = entry;

                // Register panel types
                foreach (var panelType in metadata.PanelTypes)
                {
                    var typeId = panelType.TypeId.ToLowerInvariant();

                    // Check for panel type collision (H008 fix)
                    if (_panelTypeToPlugin.TryGetValue(typeId, out var existingPluginId))
                    {
                        _logger.LogWarning(
                            "Panel type '{TypeId}' already registered by plugin '{ExistingPlugin}', " +
                            "overwriting with plugin '{NewPlugin}'",
                            typeId, existingPluginId, metadata.Id);
                    }

                    _panelTypeToPlugin[typeId] = metadata.Id;

                    // Also register prefix pattern if present
                    if (!string.IsNullOrEmpty(panelType.PrefixPattern))
                    {
                        var prefix = panelType.PrefixPattern.TrimEnd(':').ToLowerInvariant();

                        // Check for prefix collision
                        if (_panelTypeToPlugin.TryGetValue(prefix, out existingPluginId) && existingPluginId != metadata.Id)
                        {
                            _logger.LogWarning(
                                "Panel prefix '{Prefix}' already registered by plugin '{ExistingPlugin}', " +
                                "overwriting with plugin '{NewPlugin}'",
                                prefix, existingPluginId, metadata.Id);
                        }

                        _panelTypeToPlugin[prefix] = metadata.Id;
                    }
                }

                _logger.LogDebug("Discovered plugin: {PluginId} v{Version} ({Type})",
                    metadata.Id, metadata.Version, isBuiltIn ? "built-in" : "user");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read plugin manifest: {Path}", manifestPath);
            }
        }
    }

    /// <summary>
    /// Finds which plugin provides a panel type.
    /// </summary>
    public string? FindPluginForPanelType(string panelTypeId)
    {
        var typeId = panelTypeId.ToLowerInvariant();

        // Direct match
        if (_panelTypeToPlugin.TryGetValue(typeId, out var pluginId))
        {
            return pluginId;
        }

        // Check for prefix match (e.g., "video:path" -> "video")
        var colonIndex = typeId.IndexOf(':');
        if (colonIndex > 0)
        {
            var prefix = typeId[..colonIndex];
            if (_panelTypeToPlugin.TryGetValue(prefix, out pluginId))
            {
                return pluginId;
            }
        }

        return null;
    }

    /// <summary>
    /// Loads a plugin on demand.
    /// </summary>
    public async Task<IPanelPlugin?> LoadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        // Already loaded?
        if (_loadedPlugins.TryGetValue(pluginId, out var loadedEntry) && loadedEntry.Instance != null)
        {
            return loadedEntry.Instance;
        }

        // Find plugin
        if (!_availablePlugins.TryGetValue(pluginId, out var entry))
        {
            _logger.LogWarning("Plugin not found: {PluginId}", pluginId);
            return null;
        }

        try
        {
            _logger.LogInformation("Loading plugin: {PluginId}", pluginId);

            // Ensure shared assemblies are loaded before creating plugin context
            // This is essential for single-file deployments where SDK is embedded
            EnsureSharedAssembliesLoaded();

            // Create isolated load context
            entry.LoadContext = new PluginLoadContext(entry.AssemblyPath);

            // Load assembly
            var assembly = entry.LoadContext.LoadFromAssemblyPath(entry.AssemblyPath);

            // Find IPanelPlugin implementation
            var pluginType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IPanelPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

            if (pluginType == null)
            {
                throw new PluginLoadException(pluginId, PluginLoadFailureReason.PluginTypeNotFound,
                    "No IPanelPlugin implementation found in assembly");
            }

            // Create instance
            entry.Instance = (IPanelPlugin)Activator.CreateInstance(pluginType)!;

            // Create context and initialize
            var context = new PluginContext(pluginId, _loggerFactory, _services);
            await entry.Instance.InitializeAsync(context, cancellationToken).ConfigureAwait(false);

            _loadedPlugins[pluginId] = entry;
            _logger.LogInformation("Loaded plugin: {PluginId} v{Version}",
                pluginId, entry.Instance.Version);

            return entry.Instance;
        }
        catch (PluginLoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Clean up on failure
            entry.LoadContext?.Unload();
            entry.LoadContext = null;
            entry.Instance = null;

            _logger.LogError(ex, "Failed to load plugin: {PluginId}", pluginId);
            throw new PluginLoadException(pluginId, PluginLoadFailureReason.AssemblyLoadError,
                $"Failed to load plugin assembly: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Unloads a plugin and releases its resources.
    /// </summary>
    public void UnloadPlugin(string pluginId)
    {
        if (!_loadedPlugins.TryGetValue(pluginId, out var entry))
        {
            return;
        }

        try
        {
            entry.Instance?.Dispose();
            entry.LoadContext?.Unload();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error unloading plugin: {PluginId}", pluginId);
        }

        entry.Instance = null;
        entry.LoadContext = null;
        _loadedPlugins.Remove(pluginId);

        _logger.LogInformation("Unloaded plugin: {PluginId}", pluginId);
    }

    /// <summary>
    /// Gets detailed info about discovered plugins for debugging.
    /// </summary>
    public IReadOnlyList<PluginDebugInfo> GetDiscoveredPlugins()
    {
        return _availablePlugins.Values.Select(e => new PluginDebugInfo
        {
            Id = e.PluginId,
            Name = e.Metadata.Name,
            Directory = e.PluginDirectory,
            AssemblyPath = e.AssemblyPath,
            IsBuiltIn = e.IsBuiltIn,
            PanelTypes = e.Metadata.PanelTypes.Select(p => p.TypeId).ToList()
        }).ToList();
    }

    /// <summary>
    /// Gets all available panel types across all discovered plugins.
    /// </summary>
    public IEnumerable<(string PluginId, PluginPanelTypeMetadata PanelType)> GetAvailablePanelTypes()
    {
        foreach (var (pluginId, entry) in _availablePlugins)
        {
            foreach (var panelType in entry.Metadata.PanelTypes)
            {
                yield return (pluginId, panelType);
            }
        }
    }

    /// <summary>
    /// Gets a list of all available panel type IDs for display.
    /// </summary>
    public string[] GetAvailablePanelTypeIds()
    {
        return _availablePlugins.Values
            .SelectMany(e => e.Metadata.PanelTypes)
            .Select(pt => pt.PrefixPattern ?? pt.TypeId)
            .OrderBy(x => x)
            .ToArray();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var pluginId in _loadedPlugins.Keys.ToList())
        {
            UnloadPlugin(pluginId);
        }

        _disposed = true;
    }
}

/// <summary>
/// Internal state for a discovered/loaded plugin.
/// </summary>
internal sealed class PluginEntry
{
    public required string PluginId { get; init; }
    public required string PluginDirectory { get; init; }
    public required string AssemblyPath { get; init; }
    public required PluginMetadata Metadata { get; init; }
    public required bool IsBuiltIn { get; init; }
    public PluginLoadContext? LoadContext { get; set; }
    public IPanelPlugin? Instance { get; set; }
}

/// <summary>
/// Exception thrown when a plugin fails to load.
/// </summary>
public sealed class PluginLoadException : Exception
{
    public string PluginId { get; }
    public PluginLoadFailureReason Reason { get; }

    public PluginLoadException(string pluginId, PluginLoadFailureReason reason, string message, Exception? inner = null)
        : base(message, inner)
    {
        PluginId = pluginId;
        Reason = reason;
    }
}

/// <summary>
/// Reasons for plugin load failure.
/// </summary>
public enum PluginLoadFailureReason
{
    ManifestNotFound,
    ManifestInvalid,
    AssemblyNotFound,
    AssemblyLoadError,
    PluginTypeNotFound,
    InitializationFailed,
    SdkVersionMismatch,
    DependencyMissing
}

/// <summary>
/// Debug information about a discovered plugin.
/// </summary>
public sealed class PluginDebugInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Directory { get; init; }
    public required string AssemblyPath { get; init; }
    public required bool IsBuiltIn { get; init; }
    public required IReadOnlyList<string> PanelTypes { get; init; }
}
