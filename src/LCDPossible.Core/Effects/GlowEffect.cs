using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Core.Effects;

/// <summary>
/// Applies a bloom/glow effect to bright areas of the image.
/// </summary>
public sealed class GlowEffect : BaseEffect
{
    /// <inheritdoc />
    public override string EffectId => "glow";

    /// <inheritdoc />
    public override string DisplayName => "Glow";

    /// <summary>
    /// Blur radius for the glow effect. Default is 10.
    /// </summary>
    public float BlurRadius { get; set; } = 10f;

    /// <summary>
    /// Brightness threshold for glow (0-255). Pixels brighter than this will glow. Default is 180.
    /// </summary>
    public byte Threshold { get; set; } = 180;

    /// <inheritdoc />
    public override void Apply(Image<Rgba32> image)
    {
        if (!IsEnabled || Intensity <= 0) return;

        // Create a copy for the glow layer
        using var glowLayer = image.Clone();

        // Extract bright areas
        glowLayer.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    ref var pixel = ref row[x];
                    var brightness = (pixel.R + pixel.G + pixel.B) / 3;

                    if (brightness < Threshold)
                    {
                        pixel = new Rgba32(0, 0, 0, 0);
                    }
                }
            }
        });

        // Apply blur to the glow layer
        glowLayer.Mutate(ctx => ctx.GaussianBlur(BlurRadius * Intensity));

        // Blend the glow layer back onto the original using additive blending
        image.ProcessPixelRows(glowLayer, (originalAccessor, glowAccessor) =>
        {
            for (var y = 0; y < originalAccessor.Height; y++)
            {
                var originalRow = originalAccessor.GetRowSpan(y);
                var glowRow = glowAccessor.GetRowSpan(y);

                for (var x = 0; x < originalRow.Length; x++)
                {
                    ref var original = ref originalRow[x];
                    var glow = glowRow[x];

                    // Additive blend with intensity
                    original.R = ClampByte(original.R + glow.R * Intensity);
                    original.G = ClampByte(original.G + glow.G * Intensity);
                    original.B = ClampByte(original.B + glow.B * Intensity);
                }
            }
        });
    }
}
