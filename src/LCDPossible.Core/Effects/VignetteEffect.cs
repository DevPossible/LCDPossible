using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Core.Effects;

/// <summary>
/// Applies a vignette effect that darkens the edges of the image.
/// </summary>
public sealed class VignetteEffect : BaseEffect
{
    /// <inheritdoc />
    public override string EffectId => "vignette";

    /// <inheritdoc />
    public override string DisplayName => "Vignette";

    /// <summary>
    /// Radius of the vignette (0.0 = center, 1.0 = corners). Default is 0.8.
    /// </summary>
    public float Radius { get; set; } = 0.8f;

    /// <summary>
    /// Softness of the vignette edge. Default is 0.5.
    /// </summary>
    public float Softness { get; set; } = 0.5f;

    /// <inheritdoc />
    public override void Apply(Image<Rgba32> image)
    {
        if (!IsEnabled || Intensity <= 0) return;

        var width = image.Width;
        var height = image.Height;
        var centerX = width / 2f;
        var centerY = height / 2f;
        var maxDistance = MathF.Sqrt(centerX * centerX + centerY * centerY);

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var dx = (x - centerX) / centerX;
                    var dy = (y - centerY) / centerY;
                    var distance = MathF.Sqrt(dx * dx + dy * dy);

                    // Calculate vignette factor
                    var vignette = 1.0f - MathF.Pow(distance / Radius, 2.0f / Softness);
                    vignette = MathF.Max(0, MathF.Min(1, vignette));

                    // Apply intensity
                    vignette = Lerp(1.0f, vignette, Intensity);

                    ref var pixel = ref row[x];
                    pixel.R = ClampByte(pixel.R * vignette);
                    pixel.G = ClampByte(pixel.G * vignette);
                    pixel.B = ClampByte(pixel.B * vignette);
                }
            }
        });
    }
}
