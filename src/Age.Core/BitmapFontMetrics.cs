namespace Age.Core;

/// <summary>
/// Describes the layout of the built-in bitmap font: an 8 by 8 grid that covers the printable ASCII range.
/// </summary>
public static class BitmapFontMetrics
{
    /// <summary>The width of one glyph, in pixels.</summary>
    public const int GlyphWidth = 8;

    /// <summary>The height of one glyph, in pixels.</summary>
    public const int GlyphHeight = 8;

    /// <summary>The first character covered by the font.</summary>
    public const char FirstCharacter = ' ';

    /// <summary>The last character covered by the font.</summary>
    public const char LastCharacter = '~';

    /// <summary>The number of glyphs in the font grid.</summary>
    public const int GlyphCount = 95;

    /// <summary>
    /// Returns the grid index of the glyph that draws the character. Control characters, including the line feed, are rendered as space.
    /// </summary>
    public static int GetGlyphIndex(char value) =>
        value < FirstCharacter || value > LastCharacter ? 0 : value - FirstCharacter;
}
