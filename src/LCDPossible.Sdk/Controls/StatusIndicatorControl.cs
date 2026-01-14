namespace LCDPossible.Sdk.Controls;

/// <summary>
/// Status dot/badge with label for showing named states.
/// Use for: service health, connection status, alert indicators, any multi-state status.
/// </summary>
/// <example>
/// yield return new StatusIndicatorControl
/// {
///     Label = "Database",
///     Status = isHealthy ? StatusLevel.Success : StatusLevel.Error,
///     StatusText = isHealthy ? "Healthy" : "Down",
///     ColSpan = 3,
///     RowSpan = 1
/// };
/// </example>
public record StatusIndicatorControl : SemanticControl
{
    public override string ControlType => "status_indicator";

    /// <summary>Label describing what this indicator represents.</summary>
    public required string Label { get; init; }

    /// <summary>The status level determining color.</summary>
    public required StatusLevel Status { get; init; }

    /// <summary>Optional text describing the current status (auto-generated from Status if null).</summary>
    public string? StatusText { get; init; }
}
