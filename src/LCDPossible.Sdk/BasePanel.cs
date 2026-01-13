using LCDPossible.Core.Configuration;
using LCDPossible.Core.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LCDPossible.Sdk;

/// <summary>
/// Abstract base class for all display panels.
/// Provides common properties and color scheme support.
/// </summary>
/// <remarks>
/// Panel hierarchy:
/// <list type="bullet">
///   <item><see cref="BasePanel"/> - Common base for all panels</item>
///   <item><see cref="CanvasPanel"/> - For custom ImageSharp drawing (screensavers, effects)</item>
///   <item><see cref="HtmlPanel"/> - For HTML/CSS rendering via Puppeteer (info panels)</item>
///   <item><see cref="WidgetPanel"/> - For grid-based widget layouts (extends HtmlPanel)</item>
/// </list>
///
/// <para><b>Render Modes:</b></para>
/// <list type="bullet">
///   <item><see cref="PanelRenderMode.Static"/> - Render once, content never updates</item>
///   <item><see cref="PanelRenderMode.Interval"/> - Render at fixed intervals (default: 1 second)</item>
///   <item><see cref="PanelRenderMode.Stream"/> - Continuous rendering at target FPS (default: 30 FPS)</item>
/// </list>
/// </remarks>
public abstract class BasePanel : IDisplayPanel
{
    /// <summary>
    /// Color scheme for rendering. Can be updated at runtime via <see cref="SetColorScheme"/>.
    /// </summary>
    protected ResolvedColorScheme Colors { get; private set; } = ResolvedColorScheme.CreateDefault();

    /// <summary>
    /// Gets the unique identifier for this panel type.
    /// </summary>
    public abstract string PanelId { get; }

    /// <summary>
    /// Gets a human-readable name for this panel.
    /// </summary>
    public abstract string DisplayName { get; }

    /// <summary>
    /// Gets the rendering mode for this panel.
    /// Default is <see cref="PanelRenderMode.Interval"/> (update at fixed intervals).
    /// </summary>
    public virtual PanelRenderMode RenderMode => PanelRenderMode.Interval;

    /// <summary>
    /// Gets the interval between frame renders for <see cref="PanelRenderMode.Interval"/> mode.
    /// Default is 1 second. Only used when <see cref="RenderMode"/> is <see cref="PanelRenderMode.Interval"/>.
    /// </summary>
    public virtual TimeSpan RenderInterval => TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets the target frame rate for <see cref="PanelRenderMode.Stream"/> mode.
    /// Default is 30 FPS. Only used when <see cref="RenderMode"/> is <see cref="PanelRenderMode.Stream"/>.
    /// </summary>
    public virtual int TargetFrameRate => 30;

    /// <summary>
    /// Indicates whether this panel shows live/updating content.
    /// Computed from <see cref="RenderMode"/>: true unless mode is <see cref="PanelRenderMode.Static"/>.
    /// </summary>
    public bool IsLive => RenderMode != PanelRenderMode.Static;

    /// <summary>
    /// Indicates whether this panel manages its own frame timing (continuous streaming).
    /// Computed from <see cref="RenderMode"/>: true when mode is <see cref="PanelRenderMode.Stream"/>.
    /// Animated panels should not have their frames cached by the slideshow manager.
    /// </summary>
    public bool IsAnimated => RenderMode == PanelRenderMode.Stream;

    /// <summary>
    /// Tracks whether this panel has been disposed.
    /// </summary>
    protected bool _disposed;

    /// <summary>
    /// Sets the color scheme for this panel.
    /// </summary>
    /// <param name="colors">The resolved color scheme to use, or null to reset to default.</param>
    public void SetColorScheme(ResolvedColorScheme? colors)
    {
        Colors = colors ?? ResolvedColorScheme.CreateDefault();
    }

    /// <summary>
    /// Initializes the panel. Called once before rendering begins.
    /// Override to perform initialization such as loading resources.
    /// </summary>
    public virtual Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Renders the current frame for this panel.
    /// </summary>
    /// <param name="width">Target width in pixels.</param>
    /// <param name="height">Target height in pixels.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered image frame.</returns>
    public abstract Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases resources used by the panel.
    /// </summary>
    public virtual void Dispose()
    {
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
