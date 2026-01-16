using LCDPossible.Core.Services;

namespace LCDPossible.Core.Transitions;

/// <summary>
/// Default implementation of the transition registry with built-in transitions.
/// </summary>
public sealed class TransitionRegistry : ITransitionRegistry
{
    private readonly Dictionary<string, TransitionTypeInfo> _transitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly TransitionTypeInfo _defaultTransition;

    /// <summary>
    /// Creates a new transition registry with built-in transitions registered.
    /// </summary>
    public TransitionRegistry()
    {
        // Register all built-in transitions based on TransitionType enum
        RegisterTransition(TransitionType.None, "Instant", "No transition - instant switch");
        RegisterTransition(TransitionType.Fade, "Fade", "Fade from black");
        RegisterTransition(TransitionType.Crossfade, "Crossfade", "Dissolve between frames");
        RegisterTransition(TransitionType.SlideLeft, "Slide Left", "Slide in from the left");
        RegisterTransition(TransitionType.SlideRight, "Slide Right", "Slide in from the right");
        RegisterTransition(TransitionType.SlideUp, "Slide Up", "Slide in from the top");
        RegisterTransition(TransitionType.SlideDown, "Slide Down", "Slide in from the bottom");
        RegisterTransition(TransitionType.WipeLeft, "Wipe Left", "Horizontal wipe left to right");
        RegisterTransition(TransitionType.WipeRight, "Wipe Right", "Horizontal wipe right to left");
        RegisterTransition(TransitionType.WipeUp, "Wipe Up", "Vertical wipe bottom to top");
        RegisterTransition(TransitionType.WipeDown, "Wipe Down", "Vertical wipe top to bottom");
        RegisterTransition(TransitionType.ZoomIn, "Zoom In", "Zoom in from center");
        RegisterTransition(TransitionType.ZoomOut, "Zoom Out", "Zoom out from edges");
        RegisterTransition(TransitionType.PushLeft, "Push Left", "Push old frame out to the left");
        RegisterTransition(TransitionType.PushRight, "Push Right", "Push old frame out to the right");
        RegisterTransition(TransitionType.Random, "Random", "Randomly select a transition");

        _defaultTransition = _transitions["crossfade"];
    }

    private void RegisterTransition(TransitionType type, string displayName, string description)
    {
        var id = type.ToDisplayName();
        _transitions[id] = new TransitionTypeInfo(id, displayName, description);
    }

    /// <inheritdoc />
    public IReadOnlyList<TransitionTypeInfo> GetTransitionTypes()
    {
        return _transitions.Values.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public TransitionTypeInfo? GetTransition(string transitionId)
    {
        return _transitions.TryGetValue(transitionId, out var info) ? info : null;
    }

    /// <inheritdoc />
    public TransitionTypeInfo DefaultTransition => _defaultTransition;

    /// <summary>
    /// Parses a transition ID to the TransitionType enum.
    /// </summary>
    /// <param name="transitionId">The transition ID (e.g., "crossfade", "slide-left").</param>
    /// <returns>The corresponding TransitionType.</returns>
    public static TransitionType ParseType(string? transitionId)
    {
        return TransitionTypeExtensions.Parse(transitionId);
    }
}
