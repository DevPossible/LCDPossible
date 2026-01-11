using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Core.Transitions;

/// <summary>
/// Engine for applying transition effects between frames.
/// </summary>
public sealed class TransitionEngine
{
    /// <summary>
    /// Default transition duration in milliseconds.
    /// </summary>
    public const int DefaultDurationMs = 1500;

    /// <summary>
    /// Minimum transition duration in milliseconds.
    /// </summary>
    public const int MinDurationMs = 50;

    /// <summary>
    /// Maximum transition duration in milliseconds.
    /// </summary>
    public const int MaxDurationMs = 2000;

    /// <summary>
    /// Creates a black frame of the specified dimensions.
    /// </summary>
    public static Image<Rgba32> CreateBlackFrame(int width, int height)
    {
        var frame = new Image<Rgba32>(width, height);
        frame.Mutate(ctx => ctx.BackgroundColor(Color.Black));
        return frame;
    }

    /// <summary>
    /// Applies a transition effect between two frames.
    /// </summary>
    /// <param name="type">The transition type to apply.</param>
    /// <param name="previousFrame">The previous frame (null uses black).</param>
    /// <param name="nextFrame">The next frame to transition to.</param>
    /// <param name="progress">Transition progress from 0.0 to 1.0.</param>
    /// <returns>The blended frame (caller owns and must dispose).</returns>
    public static Image<Rgba32> Apply(
        TransitionType type,
        Image<Rgba32>? previousFrame,
        Image<Rgba32> nextFrame,
        float progress)
    {
        // Clamp progress to valid range
        progress = Math.Clamp(progress, 0f, 1f);

        // If progress is complete, just return a clone of next frame
        if (progress >= 1f)
        {
            return nextFrame.Clone();
        }

        // If no transition, return next frame immediately
        if (type == TransitionType.None)
        {
            return nextFrame.Clone();
        }

        // For first frame with no previous, use black
        var prev = previousFrame ?? CreateBlackFrame(nextFrame.Width, nextFrame.Height);
        var shouldDisposePrev = previousFrame == null;

        try
        {
            var result = type switch
            {
                TransitionType.Fade => ApplyFade(prev, nextFrame, progress),
                TransitionType.Crossfade => ApplyCrossfade(prev, nextFrame, progress),
                TransitionType.SlideLeft => ApplySlide(prev, nextFrame, progress, -1, 0),
                TransitionType.SlideRight => ApplySlide(prev, nextFrame, progress, 1, 0),
                TransitionType.SlideUp => ApplySlide(prev, nextFrame, progress, 0, -1),
                TransitionType.SlideDown => ApplySlide(prev, nextFrame, progress, 0, 1),
                TransitionType.WipeLeft => ApplyWipe(prev, nextFrame, progress, -1, 0),
                TransitionType.WipeRight => ApplyWipe(prev, nextFrame, progress, 1, 0),
                TransitionType.WipeDown => ApplyWipe(prev, nextFrame, progress, 0, 1),
                TransitionType.WipeUp => ApplyWipe(prev, nextFrame, progress, 0, -1),
                TransitionType.ZoomIn => ApplyZoom(prev, nextFrame, progress, true),
                TransitionType.ZoomOut => ApplyZoom(prev, nextFrame, progress, false),
                TransitionType.PushLeft => ApplyPush(prev, nextFrame, progress, -1),
                TransitionType.PushRight => ApplyPush(prev, nextFrame, progress, 1),
                _ => nextFrame.Clone()
            };

            return result;
        }
        finally
        {
            if (shouldDisposePrev)
            {
                prev.Dispose();
            }
        }
    }

    /// <summary>
    /// Fade in from black (ignores previous frame).
    /// </summary>
    private static Image<Rgba32> ApplyFade(Image<Rgba32> prev, Image<Rgba32> next, float progress)
    {
        var result = new Image<Rgba32>(next.Width, next.Height);
        result.Mutate(ctx => ctx.BackgroundColor(Color.Black));

        // Draw next frame with increasing opacity
        result.Mutate(ctx => ctx.DrawImage(next, progress));

        return result;
    }

    /// <summary>
    /// Crossfade/dissolve between frames.
    /// </summary>
    private static Image<Rgba32> ApplyCrossfade(Image<Rgba32> prev, Image<Rgba32> next, float progress)
    {
        // Start with previous frame
        var result = prev.Clone();

        // Blend in next frame with increasing opacity
        result.Mutate(ctx => ctx.DrawImage(next, progress));

        return result;
    }

    /// <summary>
    /// Slide the next frame in from a direction.
    /// </summary>
    private static Image<Rgba32> ApplySlide(
        Image<Rgba32> prev,
        Image<Rgba32> next,
        float progress,
        int dirX,
        int dirY)
    {
        // Start with previous frame
        var result = prev.Clone();

        // Calculate offset based on direction and progress
        // At progress=0, next frame is fully off-screen
        // At progress=1, next frame is fully on-screen
        var offsetX = (int)((1f - progress) * next.Width * -dirX);
        var offsetY = (int)((1f - progress) * next.Height * -dirY);

        // Draw next frame at offset position
        var point = new Point(offsetX, offsetY);
        result.Mutate(ctx => ctx.DrawImage(next, point, 1f));

        return result;
    }

    /// <summary>
    /// Wipe transition - reveal next frame from a direction.
    /// </summary>
    private static Image<Rgba32> ApplyWipe(
        Image<Rgba32> prev,
        Image<Rgba32> next,
        float progress,
        int dirX,
        int dirY)
    {
        var width = next.Width;
        var height = next.Height;
        var result = prev.Clone();

        // Calculate the wipe boundary
        if (dirX != 0)
        {
            // Horizontal wipe
            var boundary = (int)(progress * width);

            if (dirX > 0)
            {
                // Wipe from left to right - reveal from left
                if (boundary > 0)
                {
                    var cropRect = new Rectangle(0, 0, boundary, height);
                    using var cropped = next.Clone(ctx => ctx.Crop(cropRect));
                    result.Mutate(ctx => ctx.DrawImage(cropped, new Point(0, 0), 1f));
                }
            }
            else
            {
                // Wipe from right to left - reveal from right
                var startX = width - boundary;
                if (boundary > 0)
                {
                    var cropRect = new Rectangle(startX, 0, boundary, height);
                    using var cropped = next.Clone(ctx => ctx.Crop(cropRect));
                    result.Mutate(ctx => ctx.DrawImage(cropped, new Point(startX, 0), 1f));
                }
            }
        }
        else if (dirY != 0)
        {
            // Vertical wipe
            var boundary = (int)(progress * height);

            if (dirY > 0)
            {
                // Wipe from top to bottom - reveal from top
                if (boundary > 0)
                {
                    var cropRect = new Rectangle(0, 0, width, boundary);
                    using var cropped = next.Clone(ctx => ctx.Crop(cropRect));
                    result.Mutate(ctx => ctx.DrawImage(cropped, new Point(0, 0), 1f));
                }
            }
            else
            {
                // Wipe from bottom to top - reveal from bottom
                var startY = height - boundary;
                if (boundary > 0)
                {
                    var cropRect = new Rectangle(0, startY, width, boundary);
                    using var cropped = next.Clone(ctx => ctx.Crop(cropRect));
                    result.Mutate(ctx => ctx.DrawImage(cropped, new Point(0, startY), 1f));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Zoom transition.
    /// </summary>
    private static Image<Rgba32> ApplyZoom(
        Image<Rgba32> prev,
        Image<Rgba32> next,
        float progress,
        bool zoomIn)
    {
        var width = next.Width;
        var height = next.Height;

        // Start with previous frame
        var result = prev.Clone();

        float scale;
        float opacity;

        if (zoomIn)
        {
            // Zoom in: next frame starts small and grows
            scale = 0.5f + (0.5f * progress); // 0.5 -> 1.0
            opacity = progress;
        }
        else
        {
            // Zoom out: next frame starts large and shrinks to normal
            scale = 1.5f - (0.5f * progress); // 1.5 -> 1.0
            opacity = progress;
        }

        // Calculate scaled dimensions
        var scaledWidth = (int)(width * scale);
        var scaledHeight = (int)(height * scale);

        // Create scaled version of next frame
        using var scaled = next.Clone(ctx => ctx.Resize(scaledWidth, scaledHeight));

        // Calculate position to center the scaled frame
        var offsetX = (width - scaledWidth) / 2;
        var offsetY = (height - scaledHeight) / 2;

        // If the scaled image is larger than the canvas, we need to crop it
        if (scaledWidth > width || scaledHeight > height)
        {
            // Crop to fit
            var cropX = Math.Max(0, -offsetX);
            var cropY = Math.Max(0, -offsetY);
            var cropWidth = Math.Min(scaledWidth - cropX, width);
            var cropHeight = Math.Min(scaledHeight - cropY, height);

            if (cropWidth > 0 && cropHeight > 0)
            {
                using var cropped = scaled.Clone(ctx => ctx.Crop(new Rectangle(cropX, cropY, cropWidth, cropHeight)));
                var drawX = Math.Max(0, offsetX);
                var drawY = Math.Max(0, offsetY);
                result.Mutate(ctx => ctx.DrawImage(cropped, new Point(drawX, drawY), opacity));
            }
        }
        else
        {
            result.Mutate(ctx => ctx.DrawImage(scaled, new Point(offsetX, offsetY), opacity));
        }

        return result;
    }

    /// <summary>
    /// Push transition - pushes previous frame out while bringing next frame in.
    /// </summary>
    private static Image<Rgba32> ApplyPush(
        Image<Rgba32> prev,
        Image<Rgba32> next,
        float progress,
        int direction)
    {
        var width = next.Width;
        var height = next.Height;
        var result = new Image<Rgba32>(width, height);
        result.Mutate(ctx => ctx.BackgroundColor(Color.Black));

        // Calculate offsets
        var offset = (int)(progress * width);

        if (direction < 0)
        {
            // Push left: prev slides out left, next slides in from right
            var prevX = -offset;
            var nextX = width - offset;

            result.Mutate(ctx => ctx.DrawImage(prev, new Point(prevX, 0), 1f));
            result.Mutate(ctx => ctx.DrawImage(next, new Point(nextX, 0), 1f));
        }
        else
        {
            // Push right: prev slides out right, next slides in from left
            var prevX = offset;
            var nextX = -width + offset;

            result.Mutate(ctx => ctx.DrawImage(prev, new Point(prevX, 0), 1f));
            result.Mutate(ctx => ctx.DrawImage(next, new Point(nextX, 0), 1f));
        }

        return result;
    }

    /// <summary>
    /// Applies an easing function to progress for smoother transitions.
    /// </summary>
    public static float EaseInOut(float t)
    {
        // Smooth step easing
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// Applies ease-out for a decelerating effect.
    /// </summary>
    public static float EaseOut(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }
}
