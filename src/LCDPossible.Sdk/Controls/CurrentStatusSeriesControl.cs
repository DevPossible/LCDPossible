namespace LCDPossible.Sdk.Controls;

/// <summary>
/// Grid or list of current status values for multiple items.
/// Use for: CPU core usage per core, disk usage per drive, VM status list, multi-item status displays.
/// </summary>
/// <example>
/// yield return new CurrentStatusSeriesControl
/// {
///     Title = "CPU CORES",
///     Items = coreUsages.Select((u, i) => new StatusItem($"Core {i}", u)).ToList(),
///     Layout = SeriesLayout.Grid,
///     ColSpan = 6,
///     RowSpan = 2
/// };
/// </example>
public record CurrentStatusSeriesControl : SemanticControl
{
    public override string ControlType => "current_status_series";

    /// <summary>Optional title for the series section.</summary>
    public string? Title { get; init; }

    /// <summary>The status items to display.</summary>
    public required IReadOnlyList<StatusItem> Items { get; init; }

    /// <summary>Layout style for arranging the items.</summary>
    public SeriesLayout Layout { get; init; } = SeriesLayout.Grid;
}

/// <summary>
/// A single status item for CurrentStatusSeriesControl.
/// </summary>
/// <param name="Label">Item identifier/label.</param>
/// <param name="Value">Current value.</param>
/// <param name="Max">Maximum value for percentage calculation.</param>
/// <param name="Status">Optional explicit status (auto-calculated from value if Normal).</param>
public record StatusItem(string Label, double Value, double Max = 100, StatusLevel Status = StatusLevel.Normal);

/// <summary>
/// Layout variants for status series controls.
/// </summary>
public enum SeriesLayout
{
    /// <summary>Items arranged in a responsive grid.</summary>
    Grid,
    /// <summary>Items in a vertical list.</summary>
    List,
    /// <summary>Compact horizontal arrangement.</summary>
    Compact
}
