namespace LCDPossible.Sdk.Controls;

/// <summary>
/// List of label/value pairs for displaying related information.
/// Use for: status info, specifications, details, property lists.
/// </summary>
/// <example>
/// yield return new InfoListControl
/// {
///     Title = "STATUS",
///     Items = new[]
///     {
///         new InfoListItem("LOAD", "10%"),
///         new InfoListItem("POWER", "125 W"),
///         new InfoListItem("FAN", "45%", StatusLevel.Success)
///     },
///     ColSpan = 4,
///     RowSpan = 2
/// };
/// </example>
public record InfoListControl : SemanticControl
{
    public override string ControlType => "info_list";

    /// <summary>Optional title for the list section.</summary>
    public string? Title { get; init; }

    /// <summary>The label/value pairs to display.</summary>
    public required IReadOnlyList<InfoListItem> Items { get; init; }
}

/// <summary>
/// A single label/value pair for InfoListControl.
/// </summary>
/// <param name="Label">The label/key text.</param>
/// <param name="Value">The value text.</param>
/// <param name="Status">Optional status for color coding this item.</param>
public record InfoListItem(string Label, string Value, StatusLevel Status = StatusLevel.Normal);
