namespace Age.Core;

/// <summary>
/// Represents a rectangle whose origin is its top-left corner.
/// </summary>
public readonly struct Rect
{
    /// <summary>Initializes a rectangle from its top-left corner and its size.</summary>
    public Rect(Vector2 position, Vector2 size)
    {
        X = position.X;
        Y = position.Y;
        Width = size.X;
        Height = size.Y;
    }

    /// <summary>Gets the x coordinate of the top-left corner.</summary>
    public float X { get; }

    /// <summary>Gets the y coordinate of the top-left corner.</summary>
    public float Y { get; }

    /// <summary>Gets the width.</summary>
    public float Width { get; }

    /// <summary>Gets the height.</summary>
    public float Height { get; }

    /// <summary>Gets the top-left corner.</summary>
    public Vector2 Position => new(X, Y);

    /// <summary>Gets the size.</summary>
    public Vector2 Size => new(Width, Height);

    /// <summary>Returns the part of two rectangles that both of them cover, which is the whole of the one that is inside the other.</summary>
    /// <param name="other">The rectangle to intersect with.</param>
    /// <returns>The intersection. A rectangle whose size is zero when the two do not touch.</returns>
    /// <remarks>
    /// A size that is zero rather than negative is what makes a chain of intersections safe: the intersection of two
    /// rectangles that do not touch is in the same place as one of them and covers nothing, so it can be intersected again.
    /// </remarks>
    public Rect Intersect(Rect other)
    {
        float left = MathF.Max(X, other.X);
        float top = MathF.Max(Y, other.Y);
        float right = MathF.Min(X + Width, other.X + other.Width);
        float bottom = MathF.Min(Y + Height, other.Y + other.Height);

        return new Rect(new Vector2(left, top), new Vector2(MathF.Max(right - left, 0f), MathF.Max(bottom - top, 0f)));
    }
}
