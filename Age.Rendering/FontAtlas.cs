namespace Age.Rendering;

/// <summary>
/// The result of baking a font: the pixels of its glyph atlas and the metrics that place them.
/// </summary>
/// <remarks>
/// The atlas is tightly packed alpha coverage in RGBA bytes, white with the coverage in every channel, so the renderer
/// can tint it. It covers the characters that were baked, in ascending order from <see cref="First"/>.
/// </remarks>
internal sealed class FontAtlas
{
    /// <summary>Initializes a baked font.</summary>
    /// <param name="pixels">The pixels of the atlas, four RGBA bytes each.</param>
    /// <param name="width">The width of the atlas, in pixels.</param>
    /// <param name="height">The height of the atlas, in pixels.</param>
    /// <param name="glyphs">The glyphs, in the order of the character range.</param>
    /// <param name="first">The first character of the range.</param>
    /// <param name="ascent">The distance from the top of a line to the baseline, in pixels.</param>
    /// <param name="lineHeight">The distance between two baselines, in pixels.</param>
    public FontAtlas(byte[] pixels, int width, int height, FontGlyph[] glyphs, char first, float ascent, float lineHeight)
    {
        Pixels = pixels;
        Width = width;
        Height = height;
        Glyphs = glyphs;
        First = first;
        Ascent = ascent;
        LineHeight = lineHeight;
    }

    /// <summary>Gets the pixels of the atlas, four RGBA bytes each.</summary>
    public byte[] Pixels { get; }

    /// <summary>Gets the width of the atlas, in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the height of the atlas, in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets the glyphs, in the order of the character range.</summary>
    public FontGlyph[] Glyphs { get; }

    /// <summary>Gets the first character of the range that the atlas covers.</summary>
    public char First { get; }

    /// <summary>Gets the distance from the top of a line to the baseline, in pixels.</summary>
    public float Ascent { get; }

    /// <summary>Gets the distance between two baselines, in pixels.</summary>
    public float LineHeight { get; }

    /// <summary>Returns the glyph of a character, or a space when the character is outside the range of the atlas.</summary>
    /// <param name="value">The character to look up.</param>
    /// <returns>The glyph of the character. A character outside the range is drawn as a space, so text that holds one does not change the placement of the rest of the line.</returns>
    public FontGlyph Glyph(char value)
    {
        if (value >= First && value - First < Glyphs.Length)
        {
            return Glyphs[value - First];
        }

        int space = ' ' - First;
        return space >= 0 && space < Glyphs.Length ? Glyphs[space] : Glyphs[0];
    }
}
