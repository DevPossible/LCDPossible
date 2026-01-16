using LCDPossible.Core.Services;

namespace LCDPossible.Core.Effects;

/// <summary>
/// Default implementation of the effect registry with built-in effects.
/// </summary>
public sealed class EffectRegistry : IEffectRegistry, IDisposable
{
    private readonly Dictionary<string, Func<IVisualEffect>> _effectFactories = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IVisualEffect> _activeEffects = [];
    private bool _disposed;

    /// <summary>
    /// Creates a new effect registry with built-in effects registered.
    /// </summary>
    public EffectRegistry()
    {
        // Register built-in effects
        RegisterEffect("vignette", () => new VignetteEffect());
        RegisterEffect("scanlines", () => new ScanlinesEffect());
        RegisterEffect("noise", () => new NoiseEffect());
        RegisterEffect("glow", () => new GlowEffect());
        RegisterEffect("color-tint", () => new ColorTintEffect());
    }

    /// <summary>
    /// Registers an effect factory.
    /// </summary>
    /// <param name="effectId">Unique effect identifier.</param>
    /// <param name="factory">Factory function to create effect instances.</param>
    public void RegisterEffect(string effectId, Func<IVisualEffect> factory)
    {
        _effectFactories[effectId] = factory;
    }

    /// <inheritdoc />
    public IReadOnlyList<EffectTypeInfo> GetEffectTypes()
    {
        var types = new List<EffectTypeInfo>();

        foreach (var (id, factory) in _effectFactories)
        {
            using var effect = factory();
            types.Add(new EffectTypeInfo(id, effect.DisplayName));
        }

        return types;
    }

    /// <inheritdoc />
    public IVisualEffect? CreateEffect(string effectId)
    {
        return _effectFactories.TryGetValue(effectId, out var factory) ? factory() : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<IVisualEffect> ActiveEffects => _activeEffects.AsReadOnly();

    /// <inheritdoc />
    public void AddEffect(string effectId)
    {
        var effect = CreateEffect(effectId);
        if (effect != null)
        {
            _activeEffects.Add(effect);
        }
    }

    /// <inheritdoc />
    public void RemoveEffect(string effectId)
    {
        var effect = _activeEffects.FirstOrDefault(e => e.EffectId.Equals(effectId, StringComparison.OrdinalIgnoreCase));
        if (effect != null)
        {
            _activeEffects.Remove(effect);
            effect.Dispose();
        }
    }

    /// <inheritdoc />
    public void ClearEffects()
    {
        foreach (var effect in _activeEffects)
        {
            effect.Dispose();
        }
        _activeEffects.Clear();
    }

    /// <summary>
    /// Applies all active effects to an image in order.
    /// </summary>
    /// <param name="image">The image to apply effects to.</param>
    public void ApplyAll(SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> image)
    {
        foreach (var effect in _activeEffects)
        {
            if (effect.IsEnabled)
            {
                effect.Apply(image);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        ClearEffects();
        GC.SuppressFinalize(this);
    }
}
