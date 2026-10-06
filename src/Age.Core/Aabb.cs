namespace Age.Core;

/// <summary>
/// Represents an axis-aligned bounding box.
/// </summary>
public readonly struct Aabb
{
    private Aabb(float left, float top, float right, float bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    /// <summary>Gets the x coordinate of the left edge.</summary>
    public float Left { get; }

    /// <summary>Gets the y coordinate of the top edge.</summary>
    public float Top { get; }

    /// <summary>Gets the x coordinate of the right edge.</summary>
    public float Right { get; }

    /// <summary>Gets the y coordinate of the bottom edge.</summary>
    public float Bottom { get; }

    /// <summary>Creates a bounding box that exactly covers the rectangle.</summary>
    public static Aabb FromRect(Rect rect) => new(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);

    /// <summary>Determines whether this box strictly overlaps another box. Touching edges are not a collision.</summary>
    public bool Intersects(Aabb other) =>
        Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
}
