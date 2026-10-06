namespace Age.Core;

/// <summary>
/// Represents an RGBA color with 8-bit channels.
/// </summary>
public struct Color : IEquatable<Color>
{
    /// <summary>Opaque black.</summary>
    public static readonly Color Black = new(0, 0, 0);

    /// <summary>Opaque white.</summary>
    public static readonly Color White = new(255, 255, 255);

    /// <summary>Opaque red.</summary>
    public static readonly Color Red = new(255, 0, 0);

    /// <summary>Opaque green.</summary>
    public static readonly Color Green = new(0, 255, 0);

    /// <summary>Opaque blue.</summary>
    public static readonly Color Blue = new(0, 0, 255);

    /// <summary>Gets or sets the red channel.</summary>
    public byte R;

    /// <summary>Gets or sets the green channel.</summary>
    public byte G;

    /// <summary>Gets or sets the blue channel.</summary>
    public byte B;

    /// <summary>Gets or sets the alpha channel.</summary>
    public byte A;

    /// <summary>Initializes an opaque color from its red, green and blue channels.</summary>
    public Color(byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
        A = 255;
    }

    private Color(byte r, byte g, byte b, byte a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>Scales every channel by the scalar and clamps the result to the inclusive range 0 through 255.</summary>
    public static Color operator *(Color color, float scalar) =>
        new(Clamp(color.R * scalar), Clamp(color.G * scalar), Clamp(color.B * scalar), Clamp(color.A * scalar));

    /// <summary>Determines whether two colors are equal.</summary>
    public static bool operator ==(Color left, Color right) => left.Equals(right);

    /// <summary>Determines whether two colors are not equal.</summary>
    public static bool operator !=(Color left, Color right) => !left.Equals(right);

    /// <summary>Determines whether this color equals another color.</summary>
    public bool Equals(Color other) => R == other.R && G == other.G && B == other.B && A == other.A;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Color other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);

    private static byte Clamp(float value)
    {
        if (value <= 0f)
        {
            return 0;
        }

        return value >= 255f ? (byte)255 : (byte)value;
    }
}
