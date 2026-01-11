using LCDPossible.Core.Configuration;
using LCDPossible.Core.Monitoring;
using LCDPossible.Core.Rendering;
using Microsoft.Extensions.Logging;

namespace LCDPossible.Core.Plugins;

/// <summary>
/// Entry point interface for panel plugins.
/// Each plugin assembly must export exactly one implementation of this interface.
/// </summary>
public interface IPanelPlugin : IDisposable
{
    /// <summary>
    /// Unique plugin identifier (e.g., "lcdpossible.media", "community.weather").
    /// Use reverse-domain notation to avoid conflicts.
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// Human-readable plugin name displayed in UI and logs.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Plugin version.
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// Plugin author or organization name.
    /// </summary>
    string Author { get; }

    /// <summary>
    /// Minimum SDK version this plugin requires.
    /// </summary>
    Version MinimumSdkVersion { get; }

    /// <summary>
    /// Panel types provided by this plugin.
    /// Keys are panel type IDs (e.g., "video", "animated-gif").
    /// </summary>
    IReadOnlyDictionary<string, PanelTypeInfo> PanelTypes { get; }

    /// <summary>
    /// Called once when the plugin is loaded.
    /// Use this to initialize any shared resources.
    /// </summary>
    Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a panel instance for the specified type.
    /// </summary>
    IDisplayPanel? CreatePanel(string panelTypeId, PanelCreationContext context);
}

/// <summary>
/// Metadata about a panel type provided by a plugin.
/// </summary>
public sealed class PanelTypeInfo
{
    public required string TypeId { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public string? PrefixPattern { get; init; }
    public string[]? Dependencies { get; init; }
    public string? Category { get; init; }
    public bool IsLive { get; init; }
    public bool IsAnimated { get; init; }
}

/// <summary>
/// Context provided to plugins during initialization.
/// </summary>
public interface IPluginContext
{
    ILoggerFactory LoggerFactory { get; }
    string PluginDataDirectory { get; }
    Version SdkVersion { get; }
    IServiceProvider Services { get; }
    ILogger<T> CreateLogger<T>();
    ILogger CreateLogger(string categoryName);
}

/// <summary>
/// Context for creating individual panel instances.
/// </summary>
public sealed class PanelCreationContext
{
    public required string PanelTypeId { get; init; }
    public string? Argument { get; init; }
    public IReadOnlyDictionary<string, string>? Settings { get; init; }
    public ISystemInfoProvider? SystemProvider { get; init; }
    public IProxmoxProvider? ProxmoxProvider { get; init; }
    public ResolvedColorScheme? ColorScheme { get; init; }
    public ILoggerFactory? LoggerFactory { get; init; }

    public string? GetSetting(string key, string? defaultValue = null) =>
        Settings?.TryGetValue(key, out var value) == true ? value : defaultValue;

    public int GetSettingInt(string key, int defaultValue = 0) =>
        int.TryParse(GetSetting(key), out var result) ? result : defaultValue;

    public bool GetSettingBool(string key, bool defaultValue = false)
    {
        var value = GetSetting(key);
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.Ordinal) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    public float GetSettingFloat(string key, float defaultValue = 0f) =>
        float.TryParse(GetSetting(key), out var result) ? result : defaultValue;
}

/// <summary>
/// SDK version information.
/// </summary>
public static class SdkVersion
{
    public static readonly Version Current = new(1, 0, 0);
    public static readonly Version MinimumCompatible = new(1, 0, 0);

    public static bool IsCompatible(Version pluginMinVersion) => pluginMinVersion <= Current;
}
