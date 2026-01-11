using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LCDPossible.Core.Transitions;

/// <summary>
/// Interface for transition effects between frames.
/// </summary>
public interface ITransitionEffect
{
    /// <summary>
    /// Gets the transition type.
    /// </summary>
    TransitionType Type { get; }

    /// <summary>
    /// Applies the transition effect between two frames.
    /// </summary>
    /// <param name="previousFrame">The previous frame (can be null for first frame).</param>
    /// <param name="nextFrame">The next frame to transition to.</param>
    /// <param name="progress">Transition progress from 0.0 (start) to 1.0 (complete).</param>
    /// <returns>The blended frame.</returns>
    Image<Rgba32> Apply(Image<Rgba32>? previousFrame, Image<Rgba32> nextFrame, float progress);
}
