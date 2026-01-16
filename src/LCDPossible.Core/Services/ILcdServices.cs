using LCDPossible.Core.Plugins;
using LCDPossible.Core.Rendering;
using LCDPossible.Core.Sensors;

namespace LCDPossible.Core.Services;

/// <summary>
/// Unified facade for all LCD services available to panels and plugins.
/// </summary>
public interface ILcdServices : IDisposable
{
    /// <summary>
    /// Access to the sensor registry for reading hardware sensors.
    /// </summary>
    ISensorRegistry Sensors { get; }

    /// <summary>
    /// Access to the panel registry for creating and managing panels.
    /// </summary>
    IPanelRegistry Panels { get; }

    /// <summary>
    /// Access to the theme registry.
    /// </summary>
    IThemeRegistry Themes { get; }

    /// <summary>
    /// Access to the transition registry.
    /// </summary>
    ITransitionRegistry Transitions { get; }

    /// <summary>
    /// Access to the visual effects registry.
    /// </summary>
    IEffectRegistry Effects { get; }

    /// <summary>
    /// Convenience method to read a sensor value asynchronously.
    /// </summary>
    Task<T?> ReadSensorAsync<T>(string sensorId, CancellationToken ct = default);

    /// <summary>
    /// Convenience method to read a sensor's cached value synchronously.
    /// </summary>
    T? ReadSensorCached<T>(string sensorId);

    /// <summary>
    /// Convenience method to create a panel by type ID.
    /// </summary>
    IDisplayPanel? CreatePanel(string typeId, PanelCreationContext? context = null);
}
