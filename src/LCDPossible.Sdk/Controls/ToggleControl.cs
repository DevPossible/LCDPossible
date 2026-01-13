namespace LCDPossible.Sdk.Controls;

/// <summary>
/// Boolean state indicator.
/// Use for: online/offline, enabled/disabled, connected/disconnected, any true/false state.
/// </summary>
/// <example>
/// yield return new ToggleControl
/// {
///     Label = "Network",
///     Value = isConnected,
///     TrueText = "ONLINE",
///     FalseText = "OFFLINE",
///     Style = ToggleStyle.Badge,
///     ColSpan = 3,
///     RowSpan = 1
/// };
/// </example>
public record ToggleControl : SemanticControl
{
    public override string ControlType => "toggle";

    /// <summary>Label describing what this toggle represents.</summary>
    public required string Label { get; init; }

    /// <summary>The boolean state value.</summary>
    public required bool Value { get; init; }

    /// <summary>Text to display when Value is true.</summary>
    public string TrueText { get; init; } = "ON";

    /// <summary>Text to display when Value is false.</summary>
    public string FalseText { get; init; } = "OFF";

    /// <summary>Visual style for the toggle display.</summary>
    public ToggleStyle Style { get; init; } = ToggleStyle.Badge;
}

/// <summary>
/// Visual style variants for toggle controls.
/// </summary>
public enum ToggleStyle
{
    /// <summary>Colored badge with status text.</summary>
    Badge,
    /// <summary>Switch-style indicator.</summary>
    Switch,
    /// <summary>Simple colored dot.</summary>
    Dot,
    /// <summary>Text-only with color.</summary>
    Text
}
