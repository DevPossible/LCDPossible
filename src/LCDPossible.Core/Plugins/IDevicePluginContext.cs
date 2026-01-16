using Microsoft.Extensions.Logging;

namespace LCDPossible.Core.Plugins;

/// <summary>
/// Context provided to device plugins during initialization.
/// </summary>
public interface IDevicePluginContext
{
    /// <summary>
    /// Logger factory for creating loggers.
    /// </summary>
    ILoggerFactory? LoggerFactory { get; }

    /// <summary>
    /// Service provider for resolving dependencies.
    /// </summary>
    IServiceProvider? Services { get; }

    /// <summary>
    /// Path to the plugin's directory.
    /// </summary>
    string PluginDirectory { get; }

    /// <summary>
    /// The current SDK version.
    /// </summary>
    Version SdkVersion { get; }

    /// <summary>
    /// Whether debug mode is enabled.
    /// </summary>
    bool IsDebugMode { get; }
}
