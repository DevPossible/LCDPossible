using LCDPossible.Core.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LCDPossible.Core.Effects;

/// <summary>
/// Abstract base class for visual post-processing effects.
/// </summary>
public abstract class BaseEffect : IVisualEffect
{
    /// <inheritdoc />
    public abstract string EffectId { get; }

    /// <inheritdoc />
    public abstract string DisplayName { get; }

    /// <inheritdoc />
    public bool IsEnabled { get; set; } = true;

    /// <inheritdoc />
    public float Intensity { get; set; } = 1.0f;

    /// <inheritdoc />
    public abstract void Apply(Image<Rgba32> image);

    /// <summary>
    /// Linear interpolation between two values based on intensity.
    /// </summary>
    protected float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>
    /// Clamps a value between 0 and 255.
    /// </summary>
    protected byte ClampByte(float value) => (byte)Math.Clamp(value, 0, 255);

    /// <inheritdoc />
    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
