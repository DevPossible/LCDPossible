using SixLabors.ImageSharp;

namespace LCDPossible.Sdk.Layout;

/// <summary>
/// Defines the position and dimensions of a widget within the panel layout.
/// All values are in pixels, calculated proportionally from the total panel size.
/// </summary>
public readonly struct WidgetBounds : IEquatable<WidgetBounds>
{
    /// <summary>
    /// X position (pixels from left edge).
    /// </summary>
    public int X { get; init; }

    /// <summary>
    /// Y position (pixels from top edge).
    /// </summary>
    public int Y { get; init; }

    /// <summary>
    /// Widget width in pixels.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Widget height in pixels.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Size category for font scaling.
    /// </summary>
    public WidgetSize Size { get; init; }

    /// <summary>
    /// Converts bounds to an ImageSharp Rectangle.
    /// </summary>
    public Rectangle ToRectangle() => new(X, Y, Width, Height);

    /// <summary>
    /// Converts bounds to an ImageSharp RectangleF.
    /// </summary>
    public RectangleF ToRectangleF() => new(X, Y, Width, Height);

    /// <summary>
    /// Gets the center point of this widget.
    /// </summary>
    public PointF Center => new(X + Width / 2f, Y + Height / 2f);

    /// <summary>
    /// Gets the right edge X coordinate.
    /// </summary>
    public int Right => X + Width;

    /// <summary>
    /// Gets the bottom edge Y coordinate.
    /// </summary>
    public int Bottom => Y + Height;

    public bool Equals(WidgetBounds other) =>
        X == other.X && Y == other.Y &&
        Width == other.Width && Height == other.Height &&
        Size == other.Size;

    public override bool Equals(object? obj) =>
        obj is WidgetBounds other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(X, Y, Width, Height, Size);

    public static bool operator ==(WidgetBounds left, WidgetBounds right) => left.Equals(right);
    public static bool operator !=(WidgetBounds left, WidgetBounds right) => !left.Equals(right);

    public override string ToString() =>
        $"WidgetBounds({X}, {Y}, {Width}x{Height}, {Size})";
}
