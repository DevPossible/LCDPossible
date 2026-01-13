namespace LCDPossible.Core.Configuration;

/// <summary>
/// Manages page effects - loads built-in effects and provides access to them.
/// </summary>
public class PageEffectManager
{
    private static readonly Lazy<PageEffectManager> _instance = new(() => new PageEffectManager());
    private readonly Dictionary<string, PageEffect> _effects = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static PageEffectManager Instance => _instance.Value;

    private PageEffectManager()
    {
        RegisterBuiltInEffects();
    }

    /// <summary>
    /// Gets all registered effects.
    /// </summary>
    public IReadOnlyDictionary<string, PageEffect> Effects => _effects;

    /// <summary>
    /// Gets an effect by ID, or null if not found.
    /// Use "random" to get a random effect.
    /// </summary>
    public PageEffect? GetEffect(string? id)
    {
        if (string.IsNullOrEmpty(id) || id == "none")
            return null;

        if (id.Equals("random", StringComparison.OrdinalIgnoreCase))
            return GetRandomEffect();

        return _effects.TryGetValue(id, out var effect) ? effect : null;
    }

    /// <summary>
    /// Gets a random effect from all registered effects.
    /// </summary>
    public PageEffect GetRandomEffect()
    {
        var effects = _effects.Values.ToList();
        return effects[Random.Shared.Next(effects.Count)];
    }

    /// <summary>
    /// Gets a random effect from a specific category.
    /// </summary>
    public PageEffect? GetRandomEffectFromCategory(PageEffectCategory category)
    {
        var effects = _effects.Values.Where(e => e.Category == category).ToList();
        if (effects.Count == 0) return null;
        return effects[Random.Shared.Next(effects.Count)];
    }

    /// <summary>
    /// Gets all effects in a category.
    /// </summary>
    public IEnumerable<PageEffect> GetEffectsByCategory(PageEffectCategory category)
    {
        return _effects.Values.Where(e => e.Category == category);
    }

    /// <summary>
    /// Registers a custom effect.
    /// </summary>
    public void RegisterEffect(PageEffect effect)
    {
        _effects[effect.Id] = effect;
    }

    private void RegisterBuiltInEffects()
    {
        // Value Change Effects
        Register("glow-on-change", "Glow on Change",
            "Values that changed since last frame emit a brief glow/pulse",
            PageEffectCategory.ValueChange);

        Register("flip-digits", "Flip Digits",
            "Numbers flip like an airport departure board when changing",
            PageEffectCategory.ValueChange);

        Register("slide-numbers", "Slide Numbers",
            "Digits slide up/down like a slot machine when values change",
            PageEffectCategory.ValueChange);

        Register("typewriter", "Typewriter",
            "Text types out character by character on change",
            PageEffectCategory.ValueChange);

        Register("particle-burst", "Particle Burst",
            "Particles burst from widgets when values change significantly",
            PageEffectCategory.ValueChange);

        // Container Animation Effects
        Register("gentle-float", "Gentle Float",
            "Containers float up/down subtly (breathing effect)",
            PageEffectCategory.ContainerAnimation);

        Register("tilt-3d", "3D Tilt",
            "Containers have slight 3D tilt/perspective that shifts",
            PageEffectCategory.ContainerAnimation);

        Register("shake-on-warning", "Shake on Warning",
            "Containers shake when values hit warning/critical thresholds",
            PageEffectCategory.ContainerAnimation);

        Register("bounce-in", "Bounce In",
            "Widgets bounce in when panel first loads",
            PageEffectCategory.ContainerAnimation);

        Register("wave", "Wave",
            "Widgets wave in a sine pattern across the grid",
            PageEffectCategory.ContainerAnimation);

        // Background/Overlay Effects
        Register("scanlines", "Scanlines",
            "CRT/retro scanline overlay",
            PageEffectCategory.BackgroundOverlay);

        Register("matrix-rain", "Matrix Rain",
            "Digital rain falling behind widgets",
            PageEffectCategory.BackgroundOverlay);

        Register("particle-field", "Particle Field",
            "Floating particles in the background",
            PageEffectCategory.BackgroundOverlay);

        Register("grid-pulse", "Grid Pulse",
            "Grid lines pulse outward from center",
            PageEffectCategory.BackgroundOverlay);

        Register("hologram", "Hologram",
            "Holographic shimmer/interference pattern",
            PageEffectCategory.BackgroundOverlay);

        // Character/Mascot Effects
        Register("vanna-white", "Vanna White",
            "Character walks up to tiles and gestures at them",
            PageEffectCategory.CharacterMascot);

        Register("pixel-mascot", "Pixel Mascot",
            "Retro pixel character reacts to values",
            PageEffectCategory.CharacterMascot);

        Register("robot-assistant", "Robot Assistant",
            "Cute robot points at important metrics",
            PageEffectCategory.CharacterMascot);

        // Alert/Status Effects
        Register("warning-flash", "Warning Flash",
            "Panel border flashes when any value is critical",
            PageEffectCategory.AlertStatus);

        Register("spotlight", "Spotlight",
            "Roaming spotlight illuminates different widgets",
            PageEffectCategory.AlertStatus);

        Register("neon-trails", "Neon Trails",
            "Neon light trails follow value changes",
            PageEffectCategory.AlertStatus);

        Register("glitch", "Glitch",
            "Random digital glitch effects on high values",
            PageEffectCategory.AlertStatus);

        // Debug/Test Effects
        Register("red-man", "Red Man",
            "Debug overlay - full panel red at 55% opacity",
            PageEffectCategory.Other);
    }

    private void Register(string id, string displayName, string description, PageEffectCategory category)
    {
        _effects[id] = new PageEffect
        {
            Id = id,
            DisplayName = displayName,
            Description = description,
            Category = category,
            RequiresLiveMode = true
        };
    }
}
