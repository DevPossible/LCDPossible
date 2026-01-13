namespace LCDPossible.Sdk.Controls;

/// <summary>
/// Displays a single value with optional label, unit, and status.
/// Use for: temperatures, percentages, counts, names, any single data point.
/// </summary>
/// <example>
/// yield return new SingleValueControl
/// {
///     Label = "CPU USAGE",
///     Value = "47",
///     Unit = "%",
///     Size = ValueSize.Hero,
///     Status = StatusLevel.Normal,
///     ColSpan = 4,
///     RowSpan = 4
/// };
/// </example>
public record SingleValueControl : SemanticControl
{
    public override string ControlType => "single_value";

    /// <summary>Label/title displayed above or beside the value.</summary>
    public required string Label { get; init; }

    /// <summary>The primary value to display.</summary>
    public required string Value { get; init; }

    /// <summary>Optional unit suffix (e.g., "%", "°C", "GB").</summary>
    public string? Unit { get; init; }

    /// <summary>Optional subtitle or secondary text below the value.</summary>
    public string? Subtitle { get; init; }

    /// <summary>Size variant affecting text scaling.</summary>
    public ValueSize Size { get; init; } = ValueSize.Large;

    /// <summary>Status for semantic color coding.</summary>
    public StatusLevel Status { get; init; } = StatusLevel.Normal;
}
