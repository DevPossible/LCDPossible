namespace LCDPossible.Sdk.Controls;

/// <summary>
/// Linear progress bar showing a value within a range.
/// Use for: usage bars, completion indicators, capacity displays.
/// </summary>
/// <example>
/// yield return new ProgressBarControl
/// {
///     Value = 65,
///     Max = 100,
///     Label = "RAM",
///     ShowPercent = true,
///     Orientation = BarOrientation.Horizontal,
///     ColSpan = 6,
///     RowSpan = 1
/// };
/// </example>
public record ProgressBarControl : SemanticControl
{
    public override string ControlType => "progress_bar";

    /// <summary>Current value to display.</summary>
    public required double Value { get; init; }

    /// <summary>Maximum value of the range.</summary>
    public double Max { get; init; } = 100;

    /// <summary>Optional label displayed with the bar.</summary>
    public string? Label { get; init; }

    /// <summary>Whether to show the percentage value as text.</summary>
    public bool ShowPercent { get; init; } = true;

    /// <summary>Bar orientation (horizontal or vertical).</summary>
    public BarOrientation Orientation { get; init; } = BarOrientation.Horizontal;

    /// <summary>Status for semantic color coding of the bar fill.</summary>
    public StatusLevel Status { get; init; } = StatusLevel.Normal;
}

/// <summary>
/// Orientation variants for progress bars.
/// </summary>
public enum BarOrientation
{
    /// <summary>Left-to-right fill.</summary>
    Horizontal,
    /// <summary>Bottom-to-top fill.</summary>
    Vertical
}
