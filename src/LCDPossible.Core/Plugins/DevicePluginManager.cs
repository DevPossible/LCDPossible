using LCDPossible.Core.Devices;
using LCDPossible.Core.Usb;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Core.Plugins;

/// <summary>
/// Manages device plugin discovery, loading, and lifecycle.
/// </summary>
public sealed class DevicePluginManager : IDisposable
{
    private static readonly Version CurrentSdkVersion = new(1, 0, 0);

    private readonly ILogger<DevicePluginManager>? _logger;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly IServiceProvider? _services;
    private readonly bool _debug;

    // Maps (VendorId, ProductId) → PluginId
    private readonly Dictionary<(ushort, ushort), string> _deviceToPlugin = [];

    // Maps ProtocolId → PluginId
    private readonly Dictionary<string, string> _protocolToPlugin = new(StringComparer.OrdinalIgnoreCase);

    // Discovered but not yet loaded plugins
    private readonly Dictionary<string, PluginMetadata> _availablePlugins = new(StringComparer.OrdinalIgnoreCase);

    // Loaded plugin instances
    private readonly Dictionary<string, DevicePluginEntry> _loadedPlugins = new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;

    /// <summary>
    /// Creates a new device plugin manager.
    /// </summary>
    public DevicePluginManager(
        ILoggerFactory? loggerFactory = null,
        IServiceProvider? services = null,
        bool debug = false)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<DevicePluginManager>();
        _services = services;
        _debug = debug;
    }

    /// <summary>
    /// Gets the IDs of loaded device plugins.
    /// </summary>
    public IEnumerable<string> LoadedPluginIds => _loadedPlugins.Keys;

    /// <summary>
    /// Gets all discovered device plugins (loaded and unloaded).
    /// </summary>
    public IEnumerable<PluginMetadata> DiscoveredPlugins => _availablePlugins.Values;

    /// <summary>
    /// Gets all supported devices from all discovered plugins.
    /// </summary>
    public IEnumerable<SupportedDeviceInfo> GetSupportedDevices()
    {
        foreach (var metadata in _availablePlugins.Values)
        {
            foreach (var device in metadata.Devices)
            {
                yield return new SupportedDeviceInfo
                {
                    VendorId = device.GetVendorId(),
                    ProductId = device.GetProductId(),
                    DeviceName = device.Name,
                    ProtocolId = device.ProtocolId,
                    Width = device.Width,
                    Height = device.Height
                };
            }
        }
    }

    /// <summary>
    /// Gets all supported protocols from all discovered plugins.
    /// </summary>
    public IEnumerable<DeviceProtocolInfo> GetSupportedProtocols()
    {
        foreach (var metadata in _availablePlugins.Values)
        {
            foreach (var protocol in metadata.Protocols)
            {
                yield return new DeviceProtocolInfo
                {
                    ProtocolId = protocol.ProtocolId,
                    DisplayName = protocol.DisplayName,
                    DefaultPort = protocol.DefaultPort,
                    Capabilities = protocol.Capabilities is null ? new DeviceCapabilities
                    {
                        Width = 0,
                        Height = 0,
                        MaxPacketSize = 512
                    } : new DeviceCapabilities
                    {
                        Width = protocol.Capabilities.Width,
                        Height = protocol.Capabilities.Height,
                        MaxPacketSize = protocol.Capabilities.MaxPacketSize,
                        MaxFrameRate = protocol.Capabilities.MaxFrameRate,
                        SupportsBrightness = protocol.Capabilities.SupportsBrightness,
                        SupportsOrientation = protocol.Capabilities.SupportsOrientation
                    }
                };
            }
        }
    }

    /// <summary>
    /// Discovers device plugins from standard plugin directories.
    /// </summary>
    public void DiscoverPlugins()
    {
        var pluginDirs = GetPluginSearchPaths();

        foreach (var dir in pluginDirs)
        {
            if (Directory.Exists(dir))
            {
                ScanPluginDirectory(dir);
            }
        }

        _logger?.LogInformation(
            "Discovered {Count} device plugins: {Plugins}",
            _availablePlugins.Count,
            string.Join(", ", _availablePlugins.Keys));
    }

    /// <summary>
    /// Checks if a device is supported by any discovered plugin.
    /// </summary>
    public bool IsDeviceSupported(ushort vendorId, ushort productId) =>
        _deviceToPlugin.ContainsKey((vendorId, productId));

    /// <summary>
    /// Checks if a protocol is supported by any discovered plugin.
    /// </summary>
    public bool IsProtocolSupported(string protocolId) =>
        _protocolToPlugin.ContainsKey(protocolId);

    /// <summary>
    /// Gets the protocol ID for a device.
    /// </summary>
    public string? GetProtocolId(ushort vendorId, ushort productId)
    {
        if (!_deviceToPlugin.TryGetValue((vendorId, productId), out var pluginId))
            return null;

        if (!_availablePlugins.TryGetValue(pluginId, out var metadata))
            return null;

        return metadata.Devices
            .FirstOrDefault(d => d.GetVendorId() == vendorId && d.GetProductId() == productId)
            ?.ProtocolId;
    }

    /// <summary>
    /// Creates a physical device driver for the specified HID device.
    /// </summary>
    public ILcdDevice? CreatePhysicalDevice(HidDeviceInfo hidInfo, IDeviceEnumerator enumerator)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var key = (hidInfo.VendorId, hidInfo.ProductId);
        if (!_deviceToPlugin.TryGetValue(key, out var pluginId))
        {
            _logger?.LogDebug(
                "No plugin registered for device VID:0x{VendorId:X4} PID:0x{ProductId:X4}",
                hidInfo.VendorId, hidInfo.ProductId);
            return null;
        }

        var plugin = EnsurePluginLoaded(pluginId);
        if (plugin is null)
        {
            _logger?.LogWarning("Failed to load plugin {PluginId} for device", pluginId);
            return null;
        }

        try
        {
            return plugin.CreatePhysicalDevice(hidInfo, enumerator);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create physical device from plugin {PluginId}", pluginId);
            return null;
        }
    }

    /// <summary>
    /// Creates a virtual device driver for the specified protocol.
    /// </summary>
    public ILcdDevice? CreateVirtualDevice(string protocolId, string endpoint)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_protocolToPlugin.TryGetValue(protocolId, out var pluginId))
        {
            _logger?.LogDebug("No plugin registered for protocol {ProtocolId}", protocolId);
            return null;
        }

        var plugin = EnsurePluginLoaded(pluginId);
        if (plugin is null)
        {
            _logger?.LogWarning("Failed to load plugin {PluginId} for protocol", pluginId);
            return null;
        }

        try
        {
            return plugin.CreateVirtualDevice(protocolId, endpoint);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create virtual device from plugin {PluginId}", pluginId);
            return null;
        }
    }

    /// <summary>
    /// Creates a simulator handler for the specified protocol.
    /// </summary>
    public IVirtualDeviceHandler? CreateSimulatorHandler(string protocolId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_protocolToPlugin.TryGetValue(protocolId, out var pluginId))
        {
            _logger?.LogDebug("No plugin registered for protocol {ProtocolId}", protocolId);
            return null;
        }

        var plugin = EnsurePluginLoaded(pluginId);
        if (plugin is null)
        {
            _logger?.LogWarning("Failed to load plugin {PluginId} for protocol", pluginId);
            return null;
        }

        try
        {
            return plugin.CreateSimulatorHandler(protocolId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create simulator handler from plugin {PluginId}", pluginId);
            return null;
        }
    }

    /// <summary>
    /// Explicitly loads a plugin by ID.
    /// </summary>
    public async Task<IDevicePlugin?> LoadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_loadedPlugins.TryGetValue(pluginId, out var entry))
            return entry.Plugin;

        if (!_availablePlugins.TryGetValue(pluginId, out var metadata))
        {
            _logger?.LogWarning("Plugin {PluginId} not found in discovered plugins", pluginId);
            return null;
        }

        return await LoadPluginFromMetadataAsync(metadata, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Unloads a plugin and releases its resources.
    /// </summary>
    public void UnloadPlugin(string pluginId)
    {
        if (!_loadedPlugins.TryGetValue(pluginId, out var entry))
            return;

        try
        {
            entry.Plugin.Dispose();
            entry.LoadContext?.Unload();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error unloading plugin {PluginId}", pluginId);
        }

        _loadedPlugins.Remove(pluginId);
        _logger?.LogInformation("Unloaded device plugin: {PluginId}", pluginId);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var pluginId in _loadedPlugins.Keys.ToList())
        {
            UnloadPlugin(pluginId);
        }

        _loadedPlugins.Clear();
        _availablePlugins.Clear();
        _deviceToPlugin.Clear();
        _protocolToPlugin.Clear();
        _disposed = true;
    }

    private IEnumerable<string> GetPluginSearchPaths()
    {
        // Built-in plugins directory (next to executable)
        var exeDir = AppContext.BaseDirectory;
        yield return Path.Combine(exeDir, "plugins", "devices");
        yield return Path.Combine(exeDir, "plugins"); // Also check main plugins folder

        // User plugins directory
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(appData, "LCDPossible", "plugins", "devices");
    }

    private void ScanPluginDirectory(string directory)
    {
        _logger?.LogDebug("Scanning for device plugins in: {Directory}", directory);

        // Look for plugin.json files in subdirectories
        foreach (var subDir in Directory.GetDirectories(directory))
        {
            var manifestPath = Path.Combine(subDir, "plugin.json");
            if (!File.Exists(manifestPath))
                continue;

            try
            {
                var metadata = PluginMetadata.LoadFromFile(manifestPath);
                if (metadata is null)
                {
                    _logger?.LogWarning("Failed to parse plugin manifest: {Path}", manifestPath);
                    continue;
                }

                // Only process device plugins
                if (metadata.PluginType != PluginType.Device)
                {
                    _logger?.LogDebug("Skipping non-device plugin: {PluginId}", metadata.Id);
                    continue;
                }

                // Check SDK version compatibility
                var minSdkVersion = metadata.GetMinimumSdkVersion();
                if (minSdkVersion > CurrentSdkVersion)
                {
                    _logger?.LogWarning(
                        "Plugin {PluginId} requires SDK {Required}, but current is {Current}",
                        metadata.Id, minSdkVersion, CurrentSdkVersion);
                    continue;
                }

                // Register plugin
                _availablePlugins[metadata.Id] = metadata;

                // Register device mappings
                foreach (var device in metadata.Devices)
                {
                    var vid = device.GetVendorId();
                    var pid = device.GetProductId();
                    var key = (vid, pid);

                    if (_deviceToPlugin.TryGetValue(key, out var existingPlugin))
                    {
                        _logger?.LogWarning(
                            "Device VID:0x{VendorId:X4} PID:0x{ProductId:X4} already registered by {ExistingPlugin}, ignoring {NewPlugin}",
                            vid, pid, existingPlugin, metadata.Id);
                        continue;
                    }

                    _deviceToPlugin[key] = metadata.Id;
                    _logger?.LogDebug(
                        "Registered device VID:0x{VendorId:X4} PID:0x{ProductId:X4} → {PluginId}",
                        vid, pid, metadata.Id);
                }

                // Register protocol mappings
                foreach (var protocol in metadata.Protocols)
                {
                    if (_protocolToPlugin.TryGetValue(protocol.ProtocolId, out var existingPlugin))
                    {
                        _logger?.LogWarning(
                            "Protocol {ProtocolId} already registered by {ExistingPlugin}, ignoring {NewPlugin}",
                            protocol.ProtocolId, existingPlugin, metadata.Id);
                        continue;
                    }

                    _protocolToPlugin[protocol.ProtocolId] = metadata.Id;
                    _logger?.LogDebug("Registered protocol {ProtocolId} → {PluginId}", protocol.ProtocolId, metadata.Id);
                }

                _logger?.LogInformation(
                    "Discovered device plugin: {PluginId} v{Version} ({DeviceCount} devices, {ProtocolCount} protocols)",
                    metadata.Id, metadata.Version, metadata.Devices.Count, metadata.Protocols.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error scanning plugin directory: {Path}", subDir);
            }
        }
    }

    private IDevicePlugin? EnsurePluginLoaded(string pluginId)
    {
        if (_loadedPlugins.TryGetValue(pluginId, out var entry))
            return entry.Plugin;

        if (!_availablePlugins.TryGetValue(pluginId, out var metadata))
            return null;

        // Load synchronously (blocking)
        return LoadPluginFromMetadataAsync(metadata, CancellationToken.None)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    private async Task<IDevicePlugin?> LoadPluginFromMetadataAsync(
        PluginMetadata metadata,
        CancellationToken cancellationToken)
    {
        // Find the plugin directory from the manifest
        var pluginDir = _availablePlugins
            .Where(kvp => kvp.Value.Id == metadata.Id)
            .Select(kvp => GetPluginSearchPaths()
                .SelectMany(p => Directory.Exists(p) ? Directory.GetDirectories(p) : [])
                .FirstOrDefault(d => File.Exists(Path.Combine(d, "plugin.json")) &&
                    PluginMetadata.LoadFromFile(Path.Combine(d, "plugin.json"))?.Id == metadata.Id))
            .FirstOrDefault();

        if (pluginDir is null)
        {
            _logger?.LogError("Could not locate plugin directory for {PluginId}", metadata.Id);
            return null;
        }

        var assemblyPath = Path.Combine(pluginDir, metadata.AssemblyName);
        if (!File.Exists(assemblyPath))
        {
            _logger?.LogError("Plugin assembly not found: {Path}", assemblyPath);
            return null;
        }

        try
        {
            // Create isolated load context
            var loadContext = new PluginLoadContext(assemblyPath);
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

            // Find the IDevicePlugin implementation
            var pluginType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IDevicePlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

            if (pluginType is null)
            {
                _logger?.LogError("No IDevicePlugin implementation found in {Assembly}", assemblyPath);
                loadContext.Unload();
                return null;
            }

            // Create instance
            var plugin = Activator.CreateInstance(pluginType) as IDevicePlugin;
            if (plugin is null)
            {
                _logger?.LogError("Failed to create instance of {Type}", pluginType.FullName);
                loadContext.Unload();
                return null;
            }

            // Initialize the plugin
            var context = new DevicePluginContext(
                pluginDir,
                CurrentSdkVersion,
                _loggerFactory,
                _services,
                _debug);

            await plugin.InitializeAsync(context, cancellationToken).ConfigureAwait(false);

            // Store loaded plugin
            _loadedPlugins[metadata.Id] = new DevicePluginEntry
            {
                Plugin = plugin,
                LoadContext = loadContext,
                Metadata = metadata
            };

            _logger?.LogInformation("Loaded device plugin: {PluginId} v{Version}", metadata.Id, metadata.Version);
            return plugin;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load device plugin: {PluginId}", metadata.Id);
            return null;
        }
    }

    /// <summary>
    /// Entry for a loaded device plugin.
    /// </summary>
    private sealed class DevicePluginEntry
    {
        public required IDevicePlugin Plugin { get; init; }
        public PluginLoadContext? LoadContext { get; init; }
        public required PluginMetadata Metadata { get; init; }
    }
}
