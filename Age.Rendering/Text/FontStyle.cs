using Age.Core;

namespace Age.Rendering;

/// <summary>
/// One font of a text: where its file is, how tall a line of it is and which characters it is baked for.
/// </summary>
/// <remarks>
/// <para>
/// A text holds these in a stack, and the first font of the stack whose range covers a character is the one that draws it.
/// That is how one line mixes the letters of two scripts: a game that writes Russian puts the font of its game first and
/// the one that covers the Cyrillic block after it, and a game that writes Japanese adds one that covers its block. A range
/// is what a bake covers, the characters between the first and the last one, so a font that covers one block is one entry.
/// </para>
/// <para>
/// A path is baked by the font service, which caches the result by the path, the height and the range, so two texts that
/// name the same font share one atlas however many entities carry them.
/// </para>
/// </remarks>
public struct FontStyle : IEquatable<FontStyle>
{
    /// <summary>Gets or sets the path of the font file, relative to the game root.</summary>
    /// <remarks>A font that is not there, or whose file cannot be read, is reported once and leaves the characters it covers to the font below it in the stack, or to the built-in font.</remarks>
    [ResourcePath]
    public string? Path;

    /// <summary>Gets or sets the height of a line, in pixels. A font of a height of zero or less is not baked.</summary>
    public float PixelHeight;

    /// <summary>Gets or sets the first character of the range that the font is baked for. Zero means the space.</summary>
    public char FirstCharacter;

    /// <summary>Gets or sets the last character of the range that the font is baked for. Zero means the tilde, which is the printable ASCII range.</summary>
    public char LastCharacter;

    /// <summary>Gets the first character of the range, with a leave of zero answered by the space.</summary>
    public readonly char First => FirstCharacter == '\0' ? ' ' : FirstCharacter;

    /// <summary>Gets the last character of the range, with a leave of zero answered by the tilde.</summary>
    public readonly char Last => LastCharacter == '\0' ? '~' : LastCharacter;

    /// <summary>Returns a value indicating whether the range of this font covers a character.</summary>
    /// <param name="value">The character to test.</param>
    /// <returns><see langword="true"/> when a bake of this font holds a glyph for the character.</returns>
    public readonly bool Covers(char value) => value >= First && value <= Last;

    /// <summary>Determines whether another font is the same font of a stack.</summary>
    /// <param name="other">The font to compare with.</param>
    /// <returns><see langword="true"/> when the two name the same file, the same height and the same range.</returns>
    public readonly bool Equals(FontStyle other) =>
        string.Equals(Path, other.Path, StringComparison.Ordinal) &&
        PixelHeight.Equals(other.PixelHeight) &&
        FirstCharacter == other.FirstCharacter &&
        LastCharacter == other.LastCharacter;

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is FontStyle other && Equals(other);

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(Path, PixelHeight, FirstCharacter, LastCharacter);

    /// <summary>Determines whether two fonts are the same font of a stack.</summary>
    /// <param name="left">The first font.</param>
    /// <param name="right">The second font.</param>
    /// <returns><see langword="true"/> when the two are equal.</returns>
    public static bool operator ==(FontStyle left, FontStyle right) => left.Equals(right);

    /// <summary>Determines whether two fonts are different fonts of a stack.</summary>
    /// <param name="left">The first font.</param>
    /// <param name="right">The second font.</param>
    /// <returns><see langword="true"/> when the two are not equal.</returns>
    public static bool operator !=(FontStyle left, FontStyle right) => !left.Equals(right);
}
