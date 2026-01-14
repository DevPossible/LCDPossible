namespace LCDPossible.Sdk.Controls;

/// <summary>
/// Base class for all semantic controls.
/// Semantic controls define what data to display, not how to render it.
/// </summary>
public abstract record SemanticControl
{
    /// <summary>Column span in the 12-column grid (1-12).</summary>
    public int ColSpan { get; init; } = 4;

    /// <summary>Row span in the 4-row grid (1-4).</summary>
    public int RowSpan { get; init; } = 2;

    /// <summary>Unique identifier for this control type used by renderer resolution.</summary>
    public abstract string ControlType { get; }
}

/// <summary>
/// Size variants for value display controls.
/// </summary>
public enum ValueSize
{
    /// <summary>Compact text for dense layouts.</summary>
    Small,
    /// <summary>Standard readable size.</summary>
    Medium,
    /// <summary>Emphasized for primary metrics.</summary>
    Large,
    /// <summary>Extra-large for key indicators.</summary>
    XLarge,
    /// <summary>Maximum size for at-a-glance viewing from distance.</summary>
    Hero
}

/// <summary>
/// Semantic status levels for color coding.
/// </summary>
public enum StatusLevel
{
    /// <summary>Default/neutral state.</summary>
    Normal,
    /// <summary>Informational highlight.</summary>
    Info,
    /// <summary>Positive/healthy state.</summary>
    Success,
    /// <summary>Attention needed but not critical.</summary>
    Warning,
    /// <summary>Critical/error state.</summary>
    Error
}
