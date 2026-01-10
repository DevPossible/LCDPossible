using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LCDPossible.Core.Rendering;

/// <summary>
/// Interface for display panels that can render content to the LCD.
/// </summary>
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
    /// Indicates whether this panel shows live/updating content.
    /// </summary>
    bool IsLive { get; }

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
