using Microsoft.Extensions.Logging;

namespace LCDPossible.Core.Plugins;

/// <summary>
/// Implementation of <see cref="IDevicePluginContext"/> provided to device plugins.
/// </summary>
public sealed class DevicePluginContext : IDevicePluginContext
{
    /// <inheritdoc />
    public ILoggerFactory? LoggerFactory { get; }

    /// <inheritdoc />
    public IServiceProvider? Services { get; }

    /// <inheritdoc />
    public string PluginDirectory { get; }

    /// <inheritdoc />
    public Version SdkVersion { get; }

    /// <inheritdoc />
    public bool IsDebugMode { get; }

    public DevicePluginContext(
        string pluginDirectory,
        Version sdkVersion,
        ILoggerFactory? loggerFactory = null,
        IServiceProvider? services = null,
        bool isDebugMode = false)
    {
        PluginDirectory = pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory));
        SdkVersion = sdkVersion ?? throw new ArgumentNullException(nameof(sdkVersion));
        LoggerFactory = loggerFactory;
        Services = services;
        IsDebugMode = isDebugMode;
    }
}
