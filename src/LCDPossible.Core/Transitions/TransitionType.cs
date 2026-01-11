namespace LCDPossible.Core.Transitions;

/// <summary>
/// Available transition effect types for panel switching.
/// </summary>
public enum TransitionType
{
    /// <summary>
    /// No transition - instant switch.
    /// </summary>
    None,

    /// <summary>
    /// Fade from black (or previous frame).
    /// </summary>
    Fade,

    /// <summary>
    /// Crossfade/dissolve between frames.
    /// </summary>
    Crossfade,

    /// <summary>
    /// Slide in from the left.
    /// </summary>
    SlideLeft,

    /// <summary>
    /// Slide in from the right.
    /// </summary>
    SlideRight,

    /// <summary>
    /// Slide in from the top.
    /// </summary>
    SlideUp,

    /// <summary>
    /// Slide in from the bottom.
    /// </summary>
    SlideDown,

    /// <summary>
    /// Horizontal wipe from left to right.
    /// </summary>
    WipeLeft,

    /// <summary>
    /// Horizontal wipe from right to left.
    /// </summary>
    WipeRight,

    /// <summary>
    /// Vertical wipe from top to bottom.
    /// </summary>
    WipeDown,

    /// <summary>
    /// Vertical wipe from bottom to top.
    /// </summary>
    WipeUp,

    /// <summary>
    /// Zoom in from center.
    /// </summary>
    ZoomIn,

    /// <summary>
    /// Zoom out from edges.
    /// </summary>
    ZoomOut,

    /// <summary>
    /// Push the old frame out to the left.
    /// </summary>
    PushLeft,

    /// <summary>
    /// Push the old frame out to the right.
    /// </summary>
    PushRight,

    /// <summary>
    /// Randomly select a transition effect.
    /// </summary>
    Random
}

/// <summary>
/// Extension methods for TransitionType.
/// </summary>
public static class TransitionTypeExtensions
{
    private static readonly Random _random = new();

    /// <summary>
    /// All non-random transition types for random selection.
    /// </summary>
    private static readonly TransitionType[] _selectableTypes =
    [
        TransitionType.Fade,
        TransitionType.Crossfade,
        TransitionType.SlideLeft,
        TransitionType.SlideRight,
        TransitionType.SlideUp,
        TransitionType.SlideDown,
        TransitionType.WipeLeft,
        TransitionType.WipeRight,
        TransitionType.WipeDown,
        TransitionType.WipeUp,
        TransitionType.ZoomIn,
        TransitionType.ZoomOut,
        TransitionType.PushLeft,
        TransitionType.PushRight
    ];

    /// <summary>
    /// Resolves Random to an actual transition type.
    /// </summary>
    public static TransitionType Resolve(this TransitionType type)
    {
        if (type == TransitionType.Random)
        {
            return _selectableTypes[_random.Next(_selectableTypes.Length)];
        }
        return type;
    }

    /// <summary>
    /// Parses a string to TransitionType (case-insensitive).
    /// </summary>
    public static TransitionType Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TransitionType.Random;
        }

        // Normalize: remove hyphens/underscores, lowercase
        var normalized = value.Replace("-", "").Replace("_", "").ToLowerInvariant();

        return normalized switch
        {
            "none" or "instant" or "cut" => TransitionType.None,
            "fade" or "fadein" => TransitionType.Fade,
            "crossfade" or "dissolve" or "blend" => TransitionType.Crossfade,
            "slideleft" or "left" => TransitionType.SlideLeft,
            "slideright" or "right" => TransitionType.SlideRight,
            "slideup" or "up" => TransitionType.SlideUp,
            "slidedown" or "down" => TransitionType.SlideDown,
            "wipeleft" => TransitionType.WipeLeft,
            "wiperight" => TransitionType.WipeRight,
            "wipedown" => TransitionType.WipeDown,
            "wipeup" => TransitionType.WipeUp,
            "zoomin" or "zoom" => TransitionType.ZoomIn,
            "zoomout" => TransitionType.ZoomOut,
            "pushleft" => TransitionType.PushLeft,
            "pushright" => TransitionType.PushRight,
            "random" or "auto" => TransitionType.Random,
            _ => TransitionType.Random
        };
    }

    /// <summary>
    /// Gets the display name for a transition type.
    /// </summary>
    public static string ToDisplayName(this TransitionType type)
    {
        return type switch
        {
            TransitionType.None => "none",
            TransitionType.Fade => "fade",
            TransitionType.Crossfade => "crossfade",
            TransitionType.SlideLeft => "slide-left",
            TransitionType.SlideRight => "slide-right",
            TransitionType.SlideUp => "slide-up",
            TransitionType.SlideDown => "slide-down",
            TransitionType.WipeLeft => "wipe-left",
            TransitionType.WipeRight => "wipe-right",
            TransitionType.WipeDown => "wipe-down",
            TransitionType.WipeUp => "wipe-up",
            TransitionType.ZoomIn => "zoom-in",
            TransitionType.ZoomOut => "zoom-out",
            TransitionType.PushLeft => "push-left",
            TransitionType.PushRight => "push-right",
            TransitionType.Random => "random",
            _ => "random"
        };
    }
}
