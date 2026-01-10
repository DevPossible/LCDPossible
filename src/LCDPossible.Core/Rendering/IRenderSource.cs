using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LCDPossible.Core.Rendering;

/// <summary>
/// Interface for sources that provide frames to render on the LCD.
/// </summary>
public interface IRenderSource : IDisposable
{
    /// <summary>
    /// Gets whether this source produces animated content.
    /// </summary>
    bool IsAnimated { get; }

    /// <summary>
    /// Gets the duration of each frame for animated sources.
    /// </summary>
    TimeSpan FrameDuration { get; }

    /// <summary>
    /// Gets the next frame to render.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The frame image.</returns>
    Task<Image<Rgba32>> GetFrameAsync(CancellationToken cancellationToken = default);
}
