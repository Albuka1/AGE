using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Measures a text with the built-in bitmap font, which needs no file and no device.
/// </summary>
/// <remarks>
/// <para>
/// The built-in font is a grid whose glyphs are all the same size, so a character advances one width and a line is one
/// height, and neither answer needs the graphics device. That is what makes this the measurer of a text that names no font
/// at all, and what lets such a text be wrapped, aligned, cut off and measured like a text in a font of the game: the seam
/// of <see cref="ITextMeasurer"/> is the same one, and only the numbers behind it differ.
/// </para>
/// <para>
/// Every character measures the same, including the ones that the font has no glyph for: those are drawn as a space, which
/// is the width of a cell as well, so a line keeps the layout it was given.
/// </para>
/// </remarks>
public sealed class BitmapTextMeasurer : ITextMeasurer
{
    /// <summary>Gets the one measurer of this kind, which holds no state.</summary>
    public static BitmapTextMeasurer Instance { get; } = new();

    /// <inheritdoc />
    /// <remarks>The glyphs of the built-in font fill their cell, so the ascent is the height of a line and there is no descent.</remarks>
    public FontMetrics Metrics => new(BitmapFontMetrics.GlyphHeight, BitmapFontMetrics.GlyphHeight);

    /// <inheritdoc />
    public Vector2 Measure(ReadOnlySpan<char> text) =>
        new(text.Length * BitmapFontMetrics.GlyphWidth, BitmapFontMetrics.GlyphHeight);
}
