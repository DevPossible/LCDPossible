namespace LCDPossible.Core.Rendering;

/// <summary>
/// Defines how a panel renders and updates its content.
/// </summary>
public enum PanelRenderMode
{
    /// <summary>
    /// Render once on initialization. Content is static and never updates.
    /// Good for: Static images, one-time displays.
    /// The slideshow manager may cache the frame indefinitely.
    /// </summary>
    Static,

    /// <summary>
    /// Render at fixed intervals defined by <see cref="IDisplayPanel.RenderInterval"/>.
    /// Content updates periodically but not continuously.
    /// Good for: System stats, weather, sensor readings.
    /// Default interval is 1 second.
    /// </summary>
    Interval,

    /// <summary>
    /// Continuous streaming at the target frame rate defined by <see cref="IDisplayPanel.TargetFrameRate"/>.
    /// Content updates as fast as possible for smooth animations.
    /// Good for: Screensavers, animated effects, video, live visualizations.
    /// Default target is 30 FPS.
    /// </summary>
    Stream
}
