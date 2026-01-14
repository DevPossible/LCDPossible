namespace LCDPossible.Sdk.Controls;

/// <summary>
/// Circular gauge showing a value within a min/max range.
/// Use for: usage percentages, temperatures with thresholds, any bounded numeric value.
/// </summary>
/// <example>
/// yield return new GaugeControl
/// {
///     Value = 72,
///     Min = 0,
///     Max = 100,
///     Label = "CPU TEMP",
///     Unit = "°C",
///     Style = GaugeStyle.Temperature,
///     ColSpan = 4,
///     RowSpan = 2
/// };
/// </example>
public record GaugeControl : SemanticControl
{
    public override string ControlType => "gauge";

    /// <summary>Current value to display.</summary>
    public required double Value { get; init; }

    /// <summary>Minimum value of the range.</summary>
    public double Min { get; init; } = 0;

    /// <summary>Maximum value of the range.</summary>
    public double Max { get; init; } = 100;

    /// <summary>Optional label displayed with the gauge.</summary>
    public string? Label { get; init; }

    /// <summary>Optional unit suffix for the value display.</summary>
    public string? Unit { get; init; }

    /// <summary>Visual style variant for the gauge.</summary>
    public GaugeStyle Style { get; init; } = GaugeStyle.Donut;
}

/// <summary>
/// Visual style variants for gauge controls.
/// </summary>
public enum GaugeStyle
{
    /// <summary>Circular donut chart with percentage fill.</summary>
    Donut,
    /// <summary>Semi-circular arc gauge.</summary>
    Arc,
    /// <summary>Full radial dial gauge.</summary>
    Radial,
    /// <summary>Specialized temperature display with color gradients.</summary>
    Temperature
}
