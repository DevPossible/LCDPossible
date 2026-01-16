using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LCDPossible.Core.Services;

/// <summary>
/// Registry for visual post-processing effects.
/// </summary>
public interface IEffectRegistry
{
    /// <summary>
    /// Get all available effect types.
    /// </summary>
    IReadOnlyList<EffectTypeInfo> GetEffectTypes();

    /// <summary>
    /// Get an effect by ID.
    /// </summary>
    IVisualEffect? CreateEffect(string effectId);

    /// <summary>
    /// Get the currently active effects.
    /// </summary>
    IReadOnlyList<IVisualEffect> ActiveEffects { get; }

    /// <summary>
    /// Add an effect to the active effects chain.
    /// </summary>
    void AddEffect(string effectId);

    /// <summary>
    /// Remove an effect from the active effects chain.
    /// </summary>
    void RemoveEffect(string effectId);

    /// <summary>
    /// Clear all active effects.
    /// </summary>
    void ClearEffects();
}

/// <summary>
/// Effect type information.
/// </summary>
/// <param name="EffectId">Unique effect identifier.</param>
/// <param name="DisplayName">Human-readable name.</param>
/// <param name="Description">Effect description.</param>
public record EffectTypeInfo(
    string EffectId,
    string DisplayName,
    string? Description = null);

/// <summary>
/// Interface for visual post-processing effects.
/// </summary>
public interface IVisualEffect : IDisposable
{
    /// <summary>
    /// Unique effect identifier.
    /// </summary>
    string EffectId { get; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Whether the effect is currently enabled.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Effect intensity (0.0 to 1.0).
    /// </summary>
    float Intensity { get; set; }

    /// <summary>
    /// Apply the effect to an image.
    /// </summary>
    void Apply(Image<Rgba32> image);
}
