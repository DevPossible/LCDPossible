using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LCDPossible.Core.Plugins;

/// <summary>
/// Implementation of IPluginContext provided to plugins during initialization.
/// </summary>
public sealed class PluginContext : IPluginContext
{
    private readonly string _pluginId;

    /// <inheritdoc/>
    public ILoggerFactory LoggerFactory { get; }

    /// <inheritdoc/>
    public string PluginDataDirectory { get; }

    /// <inheritdoc/>
    public Version SdkVersion => Plugins.SdkVersion.Current;

    /// <inheritdoc/>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Creates a new plugin context.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="loggerFactory">Logger factory for creating loggers.</param>
    /// <param name="services">Service provider for accessing host services.</param>
    public PluginContext(string pluginId, ILoggerFactory? loggerFactory = null, IServiceProvider? services = null)
    {
        _pluginId = pluginId;
        LoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        Services = services ?? new EmptyServiceProvider();
        PluginDataDirectory = PlatformPaths.EnsurePluginDataDirectory(pluginId);
    }

    /// <inheritdoc/>
    public ILogger<T> CreateLogger<T>()
    {
        return LoggerFactory.CreateLogger<T>();
    }

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName)
    {
        return LoggerFactory.CreateLogger($"Plugin.{_pluginId}.{categoryName}");
    }

    /// <summary>
    /// Empty service provider for when no services are available.
    /// </summary>
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
