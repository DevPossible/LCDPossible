using System.Collections.Frozen;
using LCDPossible.Core.Plugins;

namespace LCDPossible.VirtualLcd.Protocols;

/// <summary>
/// Registry of available LCD protocols.
/// Supports both built-in protocols and plugin-provided protocols.
/// </summary>
public sealed class ProtocolRegistry : IDisposable
{
    private readonly FrozenDictionary<string, Func<ILcdProtocol>> _builtInFactories;
    private readonly DevicePluginManager? _pluginManager;
    private bool _disposed;

    /// <summary>
    /// Creates a protocol registry with optional plugin support.
    /// </summary>
    /// <param name="pluginManager">Optional device plugin manager for plugin-based protocols.</param>
    public ProtocolRegistry(DevicePluginManager? pluginManager = null)
    {
        _pluginManager = pluginManager;

        // Built-in protocols (fallback when plugins not available)
        var factories = new Dictionary<string, Func<ILcdProtocol>>(StringComparer.OrdinalIgnoreCase)
        {
            ["trofeo-vision"] = () => new TrofeoVisionProtocol(),
            // Add more built-in protocols here as needed
        };

        _builtInFactories = factories.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Default protocol ID.
    /// </summary>
    public const string DefaultProtocolId = "trofeo-vision";

    /// <summary>
    /// Get all available protocol IDs (from plugins and built-in).
    /// </summary>
    public IEnumerable<string> AvailableProtocols
    {
        get
        {
            var protocols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add plugin protocols first (they take priority)
            if (_pluginManager is not null)
            {
                foreach (var protocol in _pluginManager.GetSupportedProtocols())
                {
                    protocols.Add(protocol.ProtocolId);
                }
            }

            // Add built-in protocols
            foreach (var id in _builtInFactories.Keys)
            {
                protocols.Add(id);
            }

            return protocols;
        }
    }

    /// <summary>
    /// Get information about all available protocols.
    /// </summary>
    public IEnumerable<ProtocolInfo> GetProtocolInfos()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Plugin protocols first
        if (_pluginManager is not null)
        {
            foreach (var protocol in _pluginManager.GetSupportedProtocols())
            {
                seen.Add(protocol.ProtocolId);
                yield return new ProtocolInfo(
                    protocol.ProtocolId,
                    protocol.DisplayName,
                    $"Plugin-provided: {protocol.DisplayName}",
                    protocol.Capabilities.Width,
                    protocol.Capabilities.Height,
                    protocol.Capabilities.MaxPacketSize + 1, // +1 for report ID
                    0, 0); // VID/PID not exposed from plugin protocol info
            }
        }

        // Built-in protocols (skip if already provided by plugin)
        foreach (var (id, factory) in _builtInFactories)
        {
            if (seen.Contains(id))
                continue;

            using var protocol = factory();
            yield return new ProtocolInfo(
                protocol.ProtocolId,
                protocol.DisplayName,
                protocol.Description,
                protocol.Width,
                protocol.Height,
                protocol.HidReportSize,
                protocol.VendorId,
                protocol.ProductId);
        }
    }

    /// <summary>
    /// Check if a protocol ID is valid (supported by plugin or built-in).
    /// </summary>
    public bool IsValidProtocol(string protocolId)
    {
        if (_pluginManager?.IsProtocolSupported(protocolId) == true)
            return true;

        return _builtInFactories.ContainsKey(protocolId);
    }

    /// <summary>
    /// Create a protocol instance by ID.
    /// Prefers plugin-provided protocols over built-in.
    /// </summary>
    /// <param name="protocolId">Protocol identifier (case-insensitive).</param>
    /// <returns>Protocol instance.</returns>
    /// <exception cref="ArgumentException">Unknown protocol ID.</exception>
    public ILcdProtocol CreateProtocol(string protocolId)
    {
        if (TryCreateProtocol(protocolId, out var protocol))
        {
            return protocol!;
        }

        var available = string.Join(", ", AvailableProtocols);
        throw new ArgumentException($"Unknown protocol: '{protocolId}'. Available: {available}", nameof(protocolId));
    }

    /// <summary>
    /// Try to create a protocol instance by ID.
    /// </summary>
    public bool TryCreateProtocol(string protocolId, out ILcdProtocol? protocol)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Try plugin-provided handler first
        if (_pluginManager is not null)
        {
            var handler = _pluginManager.CreateSimulatorHandler(protocolId);
            if (handler is not null)
            {
                var protocolInfo = _pluginManager.GetSupportedProtocols()
                    .FirstOrDefault(p => p.ProtocolId.Equals(protocolId, StringComparison.OrdinalIgnoreCase));

                if (protocolInfo is not null)
                {
                    protocol = new PluginProtocolAdapter(handler, protocolInfo);
                    return true;
                }

                // Handler created but no protocol info - dispose and fall through
                handler.Dispose();
            }
        }

        // Fall back to built-in
        if (_builtInFactories.TryGetValue(protocolId, out var factory))
        {
            protocol = factory();
            return true;
        }

        protocol = null;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }

    #region Static Legacy API (for backward compatibility)

    private static ProtocolRegistry? _defaultInstance;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets or creates the default protocol registry instance.
    /// Call InitializeDefault() first to enable plugin support.
    /// </summary>
    public static ProtocolRegistry Default
    {
        get
        {
            if (_defaultInstance is null)
            {
                lock (_lock)
                {
                    _defaultInstance ??= new ProtocolRegistry();
                }
            }
            return _defaultInstance;
        }
    }

    /// <summary>
    /// Initialize the default registry with plugin support.
    /// </summary>
    public static void InitializeDefault(DevicePluginManager? pluginManager)
    {
        lock (_lock)
        {
            _defaultInstance?.Dispose();
            _defaultInstance = new ProtocolRegistry(pluginManager);
        }
    }

    #endregion
}

/// <summary>
/// Information about an available protocol.
/// </summary>
public readonly record struct ProtocolInfo(
    string ProtocolId,
    string DisplayName,
    string Description,
    int Width,
    int Height,
    int HidReportSize,
    ushort VendorId,
    ushort ProductId);
