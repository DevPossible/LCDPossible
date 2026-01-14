namespace LCDPossible.Sdk.Controls;

/// <summary>
/// Sparkline showing historical values over time.
/// Use for: CPU history, network throughput, temperature trends, any time-series data.
/// </summary>
/// <example>
/// yield return new HistoricalSeriesControl
/// {
///     Values = cpuHistory.TakeLast(30).ToList(),
///     Label = "CPU HISTORY",
///     Min = 0,
///     Max = 100,
///     Style = SparklineStyle.Area,
///     ColSpan = 6,
///     RowSpan = 2
/// };
/// </example>
public record HistoricalSeriesControl : SemanticControl
{
    public override string ControlType => "historical_series";

    /// <summary>The time-series values to plot (oldest to newest).</summary>
    public required IReadOnlyList<double> Values { get; init; }

    /// <summary>Optional label displayed with the chart.</summary>
    public string? Label { get; init; }

    /// <summary>Optional minimum value for Y-axis scaling (auto-calculated if null).</summary>
    public double? Min { get; init; }

    /// <summary>Optional maximum value for Y-axis scaling (auto-calculated if null).</summary>
    public double? Max { get; init; }

    /// <summary>Visual style for the sparkline.</summary>
    public SparklineStyle Style { get; init; } = SparklineStyle.Line;
}

/// <summary>
/// Visual style variants for sparklines/historical series.
/// </summary>
public enum SparklineStyle
{
    /// <summary>Simple line chart.</summary>
    Line,
    /// <summary>Filled area chart.</summary>
    Area,
    /// <summary>Bar/column chart.</summary>
    Bar
}
