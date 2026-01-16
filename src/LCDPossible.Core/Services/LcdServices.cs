using LCDPossible.Core.Effects;
using LCDPossible.Core.Plugins;
using LCDPossible.Core.Rendering;
using LCDPossible.Core.Sensors;
using LCDPossible.Core.Transitions;

namespace LCDPossible.Core.Services;

/// <summary>
/// Default implementation of ILcdServices that wires together all registries.
/// </summary>
public sealed class LcdServices : ILcdServices
{
    private bool _disposed;

    public ISensorRegistry Sensors { get; }
    public IPanelRegistry Panels { get; }
    public IThemeRegistry Themes { get; }
    public ITransitionRegistry Transitions { get; }
    public IEffectRegistry Effects { get; }

    public LcdServices(
        ISensorRegistry sensors,
        IPanelRegistry panels,
        IThemeRegistry themes,
        ITransitionRegistry transitions,
        IEffectRegistry effects)
    {
        Sensors = sensors;
        Panels = panels;
        Themes = themes;
        Transitions = transitions;
        Effects = effects;
    }

    /// <summary>
    /// Creates a minimal LcdServices with only sensor registry.
    /// Useful for testing or minimal configurations.
    /// </summary>
    public static LcdServices CreateMinimal(ISensorRegistry? sensors = null)
    {
        return new LcdServices(
            sensors ?? new SensorRegistry(),
            new NullPanelRegistry(),
            new NullThemeRegistry(),
            new NullTransitionRegistry(),
            new NullEffectRegistry());
    }

    /// <summary>
    /// Creates LcdServices with default registries including effects and transitions.
    /// </summary>
    public static LcdServices CreateDefault(ISensorRegistry? sensors = null)
    {
        return new LcdServices(
            sensors ?? new SensorRegistry(),
            new NullPanelRegistry(),
            new NullThemeRegistry(),
            new TransitionRegistry(),
            new EffectRegistry());
    }

    public Task<T?> ReadSensorAsync<T>(string sensorId, CancellationToken ct = default)
    {
        return Sensors.ReadAsync<T>(sensorId, ct);
    }

    public T? ReadSensorCached<T>(string sensorId)
    {
        return Sensors.ReadCached<T>(sensorId);
    }

    public IDisplayPanel? CreatePanel(string typeId, PanelCreationContext? context = null)
    {
        return Panels.CreatePanel(typeId, context);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Sensors.Dispose();

        if (Panels is IDisposable disposablePanels)
            disposablePanels.Dispose();

        if (Themes is IDisposable disposableThemes)
            disposableThemes.Dispose();

        if (Transitions is IDisposable disposableTransitions)
            disposableTransitions.Dispose();

        if (Effects is IDisposable disposableEffects)
            disposableEffects.Dispose();
    }
}

/// <summary>
/// Null object pattern implementation for panel registry.
/// </summary>
internal sealed class NullPanelRegistry : IPanelRegistry
{
    public IReadOnlyList<PanelTypeInfo> GetPanelTypes() => [];
    public IReadOnlyList<PanelTypeInfo> GetPanelTypesByCategory(string category) => [];
    public PanelTypeInfo? ResolvePanelType(string typeIdOrAlias) => null;
    public IDisplayPanel? CreatePanel(string typeId, PanelCreationContext? context = null) => null;
}

/// <summary>
/// Null object pattern implementation for theme registry.
/// </summary>
internal sealed class NullThemeRegistry : IThemeRegistry
{
    private static readonly ThemeInfo DefaultTheme = new("default", "Default", "System", "Default theme");

    public ThemeInfo CurrentTheme => DefaultTheme;
    public IReadOnlyList<ThemeInfo> GetThemes() => [DefaultTheme];
    public ThemeInfo? GetTheme(string themeId) => themeId == "default" ? DefaultTheme : null;
    public void SetTheme(string themeId) { }
}

/// <summary>
/// Null object pattern implementation for transition registry.
/// </summary>
internal sealed class NullTransitionRegistry : ITransitionRegistry
{
    private static readonly TransitionTypeInfo _defaultTransition = new("fade", "Fade", "Simple fade transition");

    public IReadOnlyList<TransitionTypeInfo> GetTransitionTypes() => [_defaultTransition];
    public TransitionTypeInfo? GetTransition(string transitionId) => transitionId == "fade" ? _defaultTransition : null;
    public TransitionTypeInfo DefaultTransition => _defaultTransition;
}

/// <summary>
/// Null object pattern implementation for effect registry.
/// </summary>
internal sealed class NullEffectRegistry : IEffectRegistry
{
    public IReadOnlyList<EffectTypeInfo> GetEffectTypes() => [];
    public IVisualEffect? CreateEffect(string effectId) => null;
    public IReadOnlyList<IVisualEffect> ActiveEffects => [];
    public void AddEffect(string effectId) { }
    public void RemoveEffect(string effectId) { }
    public void ClearEffects() { }
}
