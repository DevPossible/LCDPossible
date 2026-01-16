using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LCDPossible.Core.Effects;

/// <summary>
/// Applies horizontal scanlines for a CRT/retro display look.
/// </summary>
public sealed class ScanlinesEffect : BaseEffect
{
    /// <inheritdoc />
    public override string EffectId => "scanlines";

    /// <inheritdoc />
    public override string DisplayName => "Scanlines";

    /// <summary>
    /// Spacing between scanlines in pixels. Default is 2.
    /// </summary>
    public int Spacing { get; set; } = 2;

    /// <summary>
    /// Darkness of the scanlines (0.0 = invisible, 1.0 = black). Default is 0.3.
    /// </summary>
    public float Darkness { get; set; } = 0.3f;

    /// <inheritdoc />
    public override void Apply(Image<Rgba32> image)
    {
        if (!IsEnabled || Intensity <= 0) return;

        var effectiveDarkness = Darkness * Intensity;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                // Apply scanline on every Nth row
                if (y % Spacing != 0) continue;

                var row = accessor.GetRowSpan(y);
                var multiplier = 1.0f - effectiveDarkness;

                for (var x = 0; x < row.Length; x++)
                {
                    ref var pixel = ref row[x];
                    pixel.R = ClampByte(pixel.R * multiplier);
                    pixel.G = ClampByte(pixel.G * multiplier);
                    pixel.B = ClampByte(pixel.B * multiplier);
                }
            }
        });
    }
}
