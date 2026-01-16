using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LCDPossible.Core.Effects;

/// <summary>
/// Applies a color tint/overlay to the image.
/// </summary>
public sealed class ColorTintEffect : BaseEffect
{
    /// <inheritdoc />
    public override string EffectId => "color-tint";

    /// <inheritdoc />
    public override string DisplayName => "Color Tint";

    /// <summary>
    /// The tint color. Default is cyan (0, 212, 255).
    /// </summary>
    public Rgba32 TintColor { get; set; } = new(0, 212, 255);

    /// <summary>
    /// Blend mode: "multiply", "overlay", or "screen". Default is "multiply".
    /// </summary>
    public string BlendMode { get; set; } = "multiply";

    /// <inheritdoc />
    public override void Apply(Image<Rgba32> image)
    {
        if (!IsEnabled || Intensity <= 0) return;

        var tintR = TintColor.R / 255f;
        var tintG = TintColor.G / 255f;
        var tintB = TintColor.B / 255f;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    ref var pixel = ref row[x];
                    var r = pixel.R / 255f;
                    var g = pixel.G / 255f;
                    var b = pixel.B / 255f;

                    float newR, newG, newB;

                    switch (BlendMode.ToLowerInvariant())
                    {
                        case "screen":
                            newR = 1 - (1 - r) * (1 - tintR);
                            newG = 1 - (1 - g) * (1 - tintG);
                            newB = 1 - (1 - b) * (1 - tintB);
                            break;

                        case "overlay":
                            newR = r < 0.5f ? 2 * r * tintR : 1 - 2 * (1 - r) * (1 - tintR);
                            newG = g < 0.5f ? 2 * g * tintG : 1 - 2 * (1 - g) * (1 - tintG);
                            newB = b < 0.5f ? 2 * b * tintB : 1 - 2 * (1 - b) * (1 - tintB);
                            break;

                        case "multiply":
                        default:
                            newR = r * tintR;
                            newG = g * tintG;
                            newB = b * tintB;
                            break;
                    }

                    // Blend with original based on intensity
                    pixel.R = ClampByte(Lerp(pixel.R, newR * 255f, Intensity));
                    pixel.G = ClampByte(Lerp(pixel.G, newG * 255f, Intensity));
                    pixel.B = ClampByte(Lerp(pixel.B, newB * 255f, Intensity));
                }
            }
        });
    }
}
