using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LCDPossible.Core.Effects;

/// <summary>
/// Applies film grain/noise effect to the image.
/// </summary>
public sealed class NoiseEffect : BaseEffect
{
    private readonly Random _random = new();

    /// <inheritdoc />
    public override string EffectId => "noise";

    /// <inheritdoc />
    public override string DisplayName => "Film Grain";

    /// <summary>
    /// Amount of noise to apply (0.0 to 1.0). Default is 0.1.
    /// </summary>
    public float Amount { get; set; } = 0.1f;

    /// <summary>
    /// Whether to use monochrome noise (true) or color noise (false). Default is true.
    /// </summary>
    public bool Monochrome { get; set; } = true;

    /// <inheritdoc />
    public override void Apply(Image<Rgba32> image)
    {
        if (!IsEnabled || Intensity <= 0) return;

        var effectiveAmount = Amount * Intensity * 255f;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    ref var pixel = ref row[x];

                    if (Monochrome)
                    {
                        var noise = (float)(_random.NextDouble() * 2 - 1) * effectiveAmount;
                        pixel.R = ClampByte(pixel.R + noise);
                        pixel.G = ClampByte(pixel.G + noise);
                        pixel.B = ClampByte(pixel.B + noise);
                    }
                    else
                    {
                        pixel.R = ClampByte(pixel.R + (float)(_random.NextDouble() * 2 - 1) * effectiveAmount);
                        pixel.G = ClampByte(pixel.G + (float)(_random.NextDouble() * 2 - 1) * effectiveAmount);
                        pixel.B = ClampByte(pixel.B + (float)(_random.NextDouble() * 2 - 1) * effectiveAmount);
                    }
                }
            }
        });
    }
}
