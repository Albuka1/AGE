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
}
