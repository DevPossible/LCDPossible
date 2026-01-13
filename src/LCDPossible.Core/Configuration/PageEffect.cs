namespace LCDPossible.Core.Configuration;

/// <summary>
/// Defines a page effect that can be applied to any widget panel.
/// Page effects are JS scripts that add visual enhancements like
/// animations, transitions, and interactive elements.
/// </summary>
public class PageEffect
{
    /// <summary>
    /// Unique identifier for this effect (e.g., "glow-on-change", "matrix-rain").
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// Brief description of what the effect does.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Category for grouping effects in UI.
    /// </summary>
    public PageEffectCategory Category { get; set; } = PageEffectCategory.Other;

    /// <summary>
    /// The JavaScript content for this effect.
    /// </summary>
    public string? ScriptContent { get; set; }

    /// <summary>
    /// Whether this effect requires live mode (continuous rendering).
    /// Most effects with animations require live mode.
    /// </summary>
    public bool RequiresLiveMode { get; set; } = true;

    /// <summary>
    /// Default options for this effect (can be overridden per-panel).
    /// </summary>
    public Dictionary<string, object> DefaultOptions { get; set; } = new();
}

/// <summary>
/// Categories for organizing page effects.
/// </summary>
public enum PageEffectCategory
{
    /// <summary>Effects that trigger on value changes.</summary>
    ValueChange,

    /// <summary>Effects that animate containers/widgets.</summary>
    ContainerAnimation,

    /// <summary>Effects that draw backgrounds or overlays.</summary>
    BackgroundOverlay,

    /// <summary>Effects featuring animated characters or mascots.</summary>
    CharacterMascot,

    /// <summary>Effects for alerts and status indicators.</summary>
    AlertStatus,

    /// <summary>Other/miscellaneous effects.</summary>
    Other
}
