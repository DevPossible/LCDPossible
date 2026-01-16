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
        // Container Animation Effects
        Register("gentle-float", "Gentle Float",
            "Containers float up/down subtly (breathing effect)",
            PageEffectCategory.ContainerAnimation);

        Register("tilt-3d", "3D Tilt",
            "Containers have slight 3D tilt/perspective that shifts",
            PageEffectCategory.ContainerAnimation);

        Register("bounce", "Bounce",
            "Widgets bounce around with physics, bouncing off walls and each other",
            PageEffectCategory.ContainerAnimation);

        Register("wave", "Wave",
            "Widgets wave in a sine pattern across the grid",
            PageEffectCategory.ContainerAnimation);

        // Background Effects
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

        Register("fireworks", "Fireworks",
            "Colorful fireworks exploding in the background",
            PageEffectCategory.BackgroundOverlay);

        Register("hologram", "Hologram",
            "Holographic shimmer/interference pattern",
            PageEffectCategory.BackgroundOverlay);

        Register("aurora", "Aurora",
            "Northern lights with flowing color ribbons",
            PageEffectCategory.BackgroundOverlay);

        Register("snow", "Snow",
            "Gentle snowflakes drifting down",
            PageEffectCategory.BackgroundOverlay);

        Register("rain", "Rain",
            "Rain drops falling with splash effects",
            PageEffectCategory.BackgroundOverlay);

        Register("bubbles", "Bubbles",
            "Translucent bubbles floating upward",
            PageEffectCategory.BackgroundOverlay);

        Register("fireflies", "Fireflies",
            "Glowing particles drifting randomly",
            PageEffectCategory.BackgroundOverlay);

        Register("stars-twinkle", "Stars Twinkle",
            "Stationary twinkling starfield",
            PageEffectCategory.BackgroundOverlay);

        Register("lava-lamp", "Lava Lamp",
            "Blobby colored blobs floating",
            PageEffectCategory.BackgroundOverlay);

        Register("bokeh", "Bokeh",
            "Out-of-focus light circles drifting",
            PageEffectCategory.BackgroundOverlay);

        Register("smoke", "Smoke",
            "Wispy smoke tendrils rising",
            PageEffectCategory.BackgroundOverlay);

        Register("waves", "Waves",
            "Ocean waves flowing at bottom",
            PageEffectCategory.BackgroundOverlay);

        Register("confetti", "Confetti",
            "Colorful confetti falling continuously",
            PageEffectCategory.BackgroundOverlay);

        Register("lightning", "Lightning",
            "Occasional lightning flashes across background",
            PageEffectCategory.BackgroundOverlay);

        Register("clouds", "Clouds",
            "Slow-moving clouds drifting across",
            PageEffectCategory.BackgroundOverlay);

        Register("embers", "Embers",
            "Glowing embers floating upward",
            PageEffectCategory.BackgroundOverlay);

        Register("breathing-glow", "Breathing Glow",
            "Pulsing ambient glow around edges",
            PageEffectCategory.BackgroundOverlay);

        // Overlay Effects (on top of widgets)
        Register("vhs-static", "VHS Static",
            "VHS tape noise/tracking lines",
            PageEffectCategory.BackgroundOverlay);

        Register("film-grain", "Film Grain",
            "Old film grain texture overlay",
            PageEffectCategory.BackgroundOverlay);

        Register("lens-flare", "Lens Flare",
            "Moving lens flare effect",
            PageEffectCategory.BackgroundOverlay);

        Register("neon-border", "Neon Border",
            "Glowing pulse around widget edges",
            PageEffectCategory.BackgroundOverlay);

        Register("chromatic-aberration", "Chromatic Aberration",
            "RGB split/shift effect",
            PageEffectCategory.BackgroundOverlay);

        Register("crt-warp", "CRT Warp",
            "CRT screen edge warping",
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
        Register("spotlight", "Spotlight",
            "Roaming spotlight illuminates different widgets",
            PageEffectCategory.AlertStatus);
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
