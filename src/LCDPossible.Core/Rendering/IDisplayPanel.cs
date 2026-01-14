using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LCDPossible.Core.Rendering;

/// <summary>
/// Interface for display panels that can render content to the LCD.
/// </summary>
/// <remarks>
/// <para><b>Render Modes:</b></para>
/// <list type="bullet">
///   <item><see cref="PanelRenderMode.Static"/> - Render once, content never updates</item>
///   <item><see cref="PanelRenderMode.Interval"/> - Render at fixed intervals (default: 1 second)</item>
///   <item><see cref="PanelRenderMode.Stream"/> - Continuous rendering at target FPS (default: 30 FPS)</item>
/// </list>
/// </remarks>
public interface IDisplayPanel : IDisposable
{
    /// <summary>
    /// Gets the unique identifier for this panel type.
    /// </summary>
    string PanelId { get; }

    /// <summary>
    /// Gets a human-readable name for this panel.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the rendering mode for this panel.
    /// Default is <see cref="PanelRenderMode.Interval"/> (update at fixed intervals).
    /// </summary>
    PanelRenderMode RenderMode => PanelRenderMode.Interval;

    /// <summary>
    /// Gets the interval between frame renders for <see cref="PanelRenderMode.Interval"/> mode.
    /// Default is 1 second. Only used when <see cref="RenderMode"/> is <see cref="PanelRenderMode.Interval"/>.
    /// </summary>
    TimeSpan RenderInterval => TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets the target frame rate for <see cref="PanelRenderMode.Stream"/> mode.
    /// Default is 30 FPS. Only used when <see cref="RenderMode"/> is <see cref="PanelRenderMode.Stream"/>.
    /// </summary>
    int TargetFrameRate => 30;

    /// <summary>
    /// Indicates whether this panel shows live/updating content.
    /// Computed from <see cref="RenderMode"/>: true unless mode is <see cref="PanelRenderMode.Static"/>.
    /// </summary>
    bool IsLive => RenderMode != PanelRenderMode.Static;

    /// <summary>
    /// Indicates whether this panel manages its own frame timing (continuous streaming).
    /// Computed from <see cref="RenderMode"/>: true when mode is <see cref="PanelRenderMode.Stream"/>.
    /// Animated panels should not have their frames cached by the slideshow manager.
    /// </summary>
    bool IsAnimated => RenderMode == PanelRenderMode.Stream;

    /// <summary>
    /// Initializes the panel. Called once before rendering begins.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders the current frame for this panel.
    /// </summary>
    /// <param name="width">Target width in pixels.</param>
    /// <param name="height">Target height in pixels.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered image frame.</returns>
    Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration for a panel in a slideshow.
/// </summary>
public sealed class PanelConfig
{
    /// <summary>
    /// Type of panel to display.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// How long to display this panel in seconds (for slideshow).
    /// </summary>
    public int DurationSeconds { get; set; } = 10;

    /// <summary>
    /// Additional panel-specific settings.
    /// </summary>
    public Dictionary<string, string> Settings { get; set; } = [];
}
