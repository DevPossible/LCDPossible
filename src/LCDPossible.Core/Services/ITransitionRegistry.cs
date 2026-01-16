namespace LCDPossible.Core.Services;

/// <summary>
/// Registry for panel transition effects.
/// </summary>
public interface ITransitionRegistry
{
    /// <summary>
    /// Get all available transition types.
    /// </summary>
    IReadOnlyList<TransitionTypeInfo> GetTransitionTypes();

    /// <summary>
    /// Get a transition by ID.
    /// </summary>
    TransitionTypeInfo? GetTransition(string transitionId);

    /// <summary>
    /// Get the default transition.
    /// </summary>
    TransitionTypeInfo DefaultTransition { get; }
}

/// <summary>
/// Transition type information.
/// </summary>
/// <param name="TransitionId">Unique transition identifier.</param>
/// <param name="DisplayName">Human-readable name.</param>
/// <param name="Description">Transition description.</param>
public record TransitionTypeInfo(
    string TransitionId,
    string DisplayName,
    string? Description = null);
