namespace Age.Core;

/// <summary>
/// Represents a two-dimensional vector with single-precision components.
/// </summary>
public struct Vector2 : IEquatable<Vector2>
{
    /// <summary>A vector whose components are both zero.</summary>
    public static readonly Vector2 Zero = new(0f, 0f);

    /// <summary>Gets or sets the horizontal component.</summary>
    public float X;

    /// <summary>Gets or sets the vertical component.</summary>
    public float Y;

    /// <summary>Initializes a vector from its components.</summary>
    public Vector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    /// <summary>Adds two vectors component by component.</summary>
    public static Vector2 operator +(Vector2 left, Vector2 right) => new(left.X + right.X, left.Y + right.Y);

    /// <summary>Subtracts two vectors component by component.</summary>
    public static Vector2 operator -(Vector2 left, Vector2 right) => new(left.X - right.X, left.Y - right.Y);

    /// <summary>Multiplies two vectors component by component.</summary>
    public static Vector2 operator *(Vector2 left, Vector2 right) => new(left.X * right.X, left.Y * right.Y);

    /// <summary>Multiplies a vector by a scalar.</summary>
    public static Vector2 operator *(Vector2 value, float scalar) => new(value.X * scalar, value.Y * scalar);

    /// <summary>Determines whether two vectors are equal.</summary>
    public static bool operator ==(Vector2 left, Vector2 right) => left.Equals(right);

    /// <summary>Determines whether two vectors are not equal.</summary>
    public static bool operator !=(Vector2 left, Vector2 right) => !left.Equals(right);

    /// <summary>Determines whether this vector equals another vector.</summary>
    public bool Equals(Vector2 other) => X == other.X && Y == other.Y;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Vector2 other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(X, Y);
}
