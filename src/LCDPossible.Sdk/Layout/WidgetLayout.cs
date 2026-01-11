namespace LCDPossible.Sdk.Layout;

/// <summary>
/// Represents a calculated layout of widgets for a panel.
/// All calculations are resolution-agnostic, using proportional sizing.
/// </summary>
public sealed class WidgetLayout
{
    /// <summary>
    /// Total width of the panel in pixels.
    /// </summary>
    public int TotalWidth { get; }

    /// <summary>
    /// Total height of the panel in pixels.
    /// </summary>
    public int TotalHeight { get; }

    /// <summary>
    /// The calculated widget bounds for each displayed item.
    /// </summary>
    public IReadOnlyList<WidgetBounds> Widgets { get; }

    /// <summary>
    /// Number of actual items that will be displayed in widgets.
    /// May be less than total items if overflow occurs.
    /// </summary>
    public int DisplayedCount { get; }

    /// <summary>
    /// Number of items that don't fit and will be shown in overflow indicator.
    /// Zero if all items fit within the layout.
    /// </summary>
    public int OverflowCount { get; }

    /// <summary>
    /// Gap between widgets in pixels.
    /// </summary>
    public int Gap { get; }

    /// <summary>
    /// True if there are more items than can be displayed.
    /// </summary>
    public bool HasOverflow => OverflowCount > 0;

    /// <summary>
    /// True if no items are available to display.
    /// </summary>
    public bool IsEmpty => DisplayedCount == 0 && OverflowCount == 0;

    private WidgetLayout(
        int totalWidth,
        int totalHeight,
        IReadOnlyList<WidgetBounds> widgets,
        int displayedCount,
        int overflowCount,
        int gap)
    {
        TotalWidth = totalWidth;
        TotalHeight = totalHeight;
        Widgets = widgets;
        DisplayedCount = displayedCount;
        OverflowCount = overflowCount;
        Gap = gap;
    }

    /// <summary>
    /// Calculates widget layout based on screen dimensions and item count.
    /// All calculations are proportional - no hardcoded pixel values.
    /// </summary>
    /// <param name="width">Total panel width in pixels.</param>
    /// <param name="height">Total panel height in pixels.</param>
    /// <param name="itemCount">Number of items to display.</param>
    /// <returns>A WidgetLayout containing calculated widget bounds.</returns>
    public static WidgetLayout Calculate(int width, int height, int itemCount)
    {
        // Gap is 1% of smaller dimension, clamped to reasonable bounds
        int gap = Math.Clamp((int)(Math.Min(width, height) * 0.01f), 4, 20);

        var widgets = new List<WidgetBounds>();
        int displayedCount;
        int overflowCount;

        switch (itemCount)
        {
            case 0:
                // Empty state - no widgets
                displayedCount = 0;
                overflowCount = 0;
                break;

            case 1:
                // Full panel - single item gets maximum space
                widgets.Add(new WidgetBounds
                {
                    X = 0,
                    Y = 0,
                    Width = width,
                    Height = height,
                    Size = WidgetSize.Full
                });
                displayedCount = 1;
                overflowCount = 0;
                break;

            case 2:
                // Side by side - two equal columns
                {
                    int halfWidth = (width - gap) / 2;
                    widgets.Add(new WidgetBounds
                    {
                        X = 0,
                        Y = 0,
                        Width = halfWidth,
                        Height = height,
                        Size = WidgetSize.Half
                    });
                    widgets.Add(new WidgetBounds
                    {
                        X = halfWidth + gap,
                        Y = 0,
                        Width = width - halfWidth - gap,
                        Height = height,
                        Size = WidgetSize.Half
                    });
                    displayedCount = 2;
                    overflowCount = 0;
                }
                break;

            case 3:
                // Left large + right stack
                {
                    int leftWidth = (width - gap) / 2;
                    int rightWidth = width - leftWidth - gap;
                    int topHeight = (height - gap) / 2;
                    int bottomHeight = height - topHeight - gap;

                    // Left widget (full height)
                    widgets.Add(new WidgetBounds
                    {
                        X = 0,
                        Y = 0,
                        Width = leftWidth,
                        Height = height,
                        Size = WidgetSize.Half
                    });

                    // Top-right widget
                    widgets.Add(new WidgetBounds
                    {
                        X = leftWidth + gap,
                        Y = 0,
                        Width = rightWidth,
                        Height = topHeight,
                        Size = WidgetSize.Quarter
                    });

                    // Bottom-right widget
                    widgets.Add(new WidgetBounds
                    {
                        X = leftWidth + gap,
                        Y = topHeight + gap,
                        Width = rightWidth,
                        Height = bottomHeight,
                        Size = WidgetSize.Quarter
                    });

                    displayedCount = 3;
                    overflowCount = 0;
                }
                break;

            default:
                // 4 or more items - 2x2 grid
                {
                    int colWidth = (width - gap) / 2;
                    int rowHeight = (height - gap) / 2;

                    // Top-left
                    widgets.Add(new WidgetBounds
                    {
                        X = 0,
                        Y = 0,
                        Width = colWidth,
                        Height = rowHeight,
                        Size = WidgetSize.Quarter
                    });

                    // Top-right
                    widgets.Add(new WidgetBounds
                    {
                        X = colWidth + gap,
                        Y = 0,
                        Width = width - colWidth - gap,
                        Height = rowHeight,
                        Size = WidgetSize.Quarter
                    });

                    // Bottom-left
                    widgets.Add(new WidgetBounds
                    {
                        X = 0,
                        Y = rowHeight + gap,
                        Width = colWidth,
                        Height = height - rowHeight - gap,
                        Size = WidgetSize.Quarter
                    });

                    // Bottom-right (either item 4 or overflow indicator)
                    widgets.Add(new WidgetBounds
                    {
                        X = colWidth + gap,
                        Y = rowHeight + gap,
                        Width = width - colWidth - gap,
                        Height = height - rowHeight - gap,
                        Size = WidgetSize.Quarter
                    });

                    if (itemCount == 4)
                    {
                        // Exactly 4 items - show all
                        displayedCount = 4;
                        overflowCount = 0;
                    }
                    else
                    {
                        // 5+ items - show 3 + overflow indicator
                        displayedCount = 3;
                        overflowCount = itemCount - 3;
                    }
                }
                break;
        }

        return new WidgetLayout(width, height, widgets, displayedCount, overflowCount, gap);
    }

    /// <summary>
    /// Gets the widget bounds at the specified index.
    /// </summary>
    public WidgetBounds GetWidget(int index)
    {
        if (index < 0 || index >= Widgets.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Widget index {index} is out of range. Available widgets: {Widgets.Count}");
        }

        return Widgets[index];
    }

    /// <summary>
    /// Gets the overflow widget bounds (the last widget when HasOverflow is true).
    /// </summary>
    public WidgetBounds GetOverflowWidget()
    {
        if (!HasOverflow)
        {
            throw new InvalidOperationException("No overflow widget available - all items fit within the layout.");
        }

        return Widgets[^1]; // Last widget is the overflow slot
    }
}
