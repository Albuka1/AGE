namespace Age.Core;

/// <summary>
/// Represents a four by four matrix stored in row-major order.
/// </summary>
public struct Matrix4x4 : IEquatable<Matrix4x4>
{
    /// <summary>The identity matrix.</summary>
    public static readonly Matrix4x4 Identity = new()
    {
        M11 = 1f,
        M22 = 1f,
        M33 = 1f,
        M44 = 1f,
    };

    /// <summary>Gets or sets the element in row 1, column 1.</summary>
    public float M11;

    /// <summary>Gets or sets the element in row 1, column 2.</summary>
    public float M12;

    /// <summary>Gets or sets the element in row 1, column 3.</summary>
    public float M13;

    /// <summary>Gets or sets the element in row 1, column 4.</summary>
    public float M14;

    /// <summary>Gets or sets the element in row 2, column 1.</summary>
    public float M21;

    /// <summary>Gets or sets the element in row 2, column 2.</summary>
    public float M22;

    /// <summary>Gets or sets the element in row 2, column 3.</summary>
    public float M23;

    /// <summary>Gets or sets the element in row 2, column 4.</summary>
    public float M24;

    /// <summary>Gets or sets the element in row 3, column 1.</summary>
    public float M31;

    /// <summary>Gets or sets the element in row 3, column 2.</summary>
    public float M32;

    /// <summary>Gets or sets the element in row 3, column 3.</summary>
    public float M33;

    /// <summary>Gets or sets the element in row 3, column 4.</summary>
    public float M34;

    /// <summary>Gets or sets the element in row 4, column 1.</summary>
    public float M41;

    /// <summary>Gets or sets the element in row 4, column 2.</summary>
    public float M42;

    /// <summary>Gets or sets the element in row 4, column 3.</summary>
    public float M43;

    /// <summary>Gets or sets the element in row 4, column 4.</summary>
    public float M44;

    /// <summary>Creates a scaling matrix.</summary>
    public static Matrix4x4 CreateScale(float x, float y, float z) => new()
    {
        M11 = x,
        M22 = y,
        M33 = z,
        M44 = 1f,
    };

    /// <summary>Creates a translation matrix.</summary>
    public static Matrix4x4 CreateTranslation(float x, float y, float z) => new()
    {
        M11 = 1f,
        M22 = 1f,
        M33 = 1f,
        M44 = 1f,
        M14 = x,
        M24 = y,
        M34 = z,
    };

    /// <summary>
    /// Creates an orthographic projection for a pixel-sized viewport whose origin is the top-left corner and whose y axis points down.
    /// </summary>
    public static Matrix4x4 CreateOrthographic(float width, float height) => new()
    {
        M11 = 2f / width,
        M22 = -2f / height,
        M33 = 1f,
        M44 = 1f,
        M41 = -1f,
        M42 = 1f,
    };

    /// <summary>Multiplies two matrices.</summary>
    public static Matrix4x4 operator *(Matrix4x4 left, Matrix4x4 right) => new()
    {
        M11 = (left.M11 * right.M11) + (left.M12 * right.M21) + (left.M13 * right.M31) + (left.M14 * right.M41),
        M12 = (left.M11 * right.M12) + (left.M12 * right.M22) + (left.M13 * right.M32) + (left.M14 * right.M42),
        M13 = (left.M11 * right.M13) + (left.M12 * right.M23) + (left.M13 * right.M33) + (left.M14 * right.M43),
        M14 = (left.M11 * right.M14) + (left.M12 * right.M24) + (left.M13 * right.M34) + (left.M14 * right.M44),
        M21 = (left.M21 * right.M11) + (left.M22 * right.M21) + (left.M23 * right.M31) + (left.M24 * right.M41),
        M22 = (left.M21 * right.M12) + (left.M22 * right.M22) + (left.M23 * right.M32) + (left.M24 * right.M42),
        M23 = (left.M21 * right.M13) + (left.M22 * right.M23) + (left.M23 * right.M33) + (left.M24 * right.M43),
        M24 = (left.M21 * right.M14) + (left.M22 * right.M24) + (left.M23 * right.M34) + (left.M24 * right.M44),
        M31 = (left.M31 * right.M11) + (left.M32 * right.M21) + (left.M33 * right.M31) + (left.M34 * right.M41),
        M32 = (left.M31 * right.M12) + (left.M32 * right.M22) + (left.M33 * right.M32) + (left.M34 * right.M42),
        M33 = (left.M31 * right.M13) + (left.M32 * right.M23) + (left.M33 * right.M33) + (left.M34 * right.M43),
        M34 = (left.M31 * right.M14) + (left.M32 * right.M24) + (left.M33 * right.M34) + (left.M34 * right.M44),
        M41 = (left.M41 * right.M11) + (left.M42 * right.M21) + (left.M43 * right.M31) + (left.M44 * right.M41),
        M42 = (left.M41 * right.M12) + (left.M42 * right.M22) + (left.M43 * right.M32) + (left.M44 * right.M42),
        M43 = (left.M41 * right.M13) + (left.M42 * right.M23) + (left.M43 * right.M33) + (left.M44 * right.M43),
        M44 = (left.M41 * right.M14) + (left.M42 * right.M24) + (left.M43 * right.M34) + (left.M44 * right.M44),
    };

    /// <summary>Determines whether two matrices are equal.</summary>
    public static bool operator ==(Matrix4x4 left, Matrix4x4 right) => left.Equals(right);

    /// <summary>Determines whether two matrices are not equal.</summary>
    public static bool operator !=(Matrix4x4 left, Matrix4x4 right) => !left.Equals(right);

    /// <summary>Determines whether this matrix equals another matrix.</summary>
    public bool Equals(Matrix4x4 other) =>
        M11 == other.M11 && M12 == other.M12 && M13 == other.M13 && M14 == other.M14 &&
        M21 == other.M21 && M22 == other.M22 && M23 == other.M23 && M24 == other.M24 &&
        M31 == other.M31 && M32 == other.M32 && M33 == other.M33 && M34 == other.M34 &&
        M41 == other.M41 && M42 == other.M42 && M43 == other.M43 && M44 == other.M44;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Matrix4x4 other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(M11);
        hash.Add(M12);
        hash.Add(M13);
        hash.Add(M14);
        hash.Add(M21);
        hash.Add(M22);
        hash.Add(M23);
        hash.Add(M24);
        hash.Add(M31);
        hash.Add(M32);
        hash.Add(M33);
        hash.Add(M34);
        hash.Add(M41);
        hash.Add(M42);
        hash.Add(M43);
        hash.Add(M44);
        return hash.ToHashCode();
    }
}
