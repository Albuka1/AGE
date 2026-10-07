using Age.Core;
using StbTrueTypeSharp;

namespace Age.Rendering;

/// <summary>
/// Bakes the glyphs of a TrueType or OpenType font into an atlas with <c>StbTrueTypeSharp</c>.
/// </summary>
/// <remarks>
/// The font is read from the bytes of the caller in one pass and never kept, so nothing stays pinned between calls. A
/// glyph is rasterized as coverage and stored as a white pixel whose alpha is that coverage, which lets the renderer tint
/// it like any other texture. The characters have to form a contiguous range, so a glyph can be found by subtracting the
/// first character of the range, which is what the text of the service relies on.
/// </remarks>
internal static class TrueTypeFontBake
{
    /// <summary>The characters of the printable ASCII range, which is the range that the built-in bitmap font covers.</summary>
    public const string AsciiCharacters = """ !"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\]^_`abcdefghijklmnopqrstuvwxyz{|}~""";

    /// <summary>Bakes the glyphs of the given characters at a pixel height.</summary>
    /// <param name="font">The bytes of the font file.</param>
    /// <param name="pixelHeight">The height of a line, in pixels. Must be greater than zero.</param>
    /// <param name="characters">The characters to bake. They have to form a contiguous range, and at least one has to be given.</param>
    /// <returns>The atlas and the metrics of the glyphs, ordered from the first character of the range.</returns>
    /// <exception cref="ArgumentException"><paramref name="font"/> does not hold a font that can be read, the characters are not a contiguous range, or none was given.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pixelHeight"/> is zero or negative.</exception>
    public static unsafe FontAtlas Bake(byte[] font, float pixelHeight, ReadOnlySpan<char> characters)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelHeight);

        char[] wanted = Range(characters);
        var info = new StbTrueType.stbtt_fontinfo();

        fixed (byte* data = font)
        {
            if (StbTrueType.stbtt_InitFont(info, data, 0) == 0)
            {
                throw new ArgumentException("The bytes do not hold a font that can be read.", nameof(font));
            }

            float scale = StbTrueType.stbtt_ScaleForPixelHeight(info, pixelHeight);
            var sizes = new Vector2[wanted.Length];
            var glyphs = new FontGlyph[wanted.Length];
            var indices = new int[wanted.Length];

            Measure(info, wanted, scale, sizes, glyphs, indices);

            var placements = new (int X, int Y)[wanted.Length];
            GlyphPacking.Place(sizes, placements, out int width, out int height);
            byte[] pixels = new byte[width * height * 4];

            for (int index = 0; index < wanted.Length; index++)
            {
                int glyphWidth = (int)sizes[index].X;
                int glyphHeight = (int)sizes[index].Y;
                int targetX = placements[index].X + GlyphPacking.Padding;
                int targetY = placements[index].Y + GlyphPacking.Padding;

                Draw(info, indices[index], glyphWidth, glyphHeight, targetX, targetY, width, scale, pixels);

                glyphs[index] = new FontGlyph(
                    new Rect(
                        new Vector2(targetX / (float)width, targetY / (float)height),
                        new Vector2(glyphWidth / (float)width, glyphHeight / (float)height)),
                    sizes[index],
                    glyphs[index].Bearing,
                    glyphs[index].Advance);
            }

            int ascent, descent, lineGap;
            StbTrueType.stbtt_GetFontVMetrics(info, &ascent, &descent, &lineGap);

            return new FontAtlas(
                pixels,
                width,
                height,
                glyphs,
                wanted[0],
                ascent * scale,
                (ascent - descent + lineGap) * scale);
        }
    }

    /// <summary>Reads the size, the bearing and the advance of every character of the range.</summary>
    private static unsafe void Measure(StbTrueType.stbtt_fontinfo info, char[] wanted, float scale, Vector2[] sizes, FontGlyph[] glyphs, int[] indices)
    {
        for (int index = 0; index < wanted.Length; index++)
        {
            int glyph = StbTrueType.stbtt_FindGlyphIndex(info, wanted[index]);

            int advanceWidth;
            StbTrueType.stbtt_GetGlyphHMetrics(info, glyph, &advanceWidth, null);

            int x0, y0, x1, y1;
            StbTrueType.stbtt_GetGlyphBitmapBox(info, glyph, scale, scale, &x0, &y0, &x1, &y1);

            indices[index] = glyph;
            sizes[index] = new Vector2(x1 - x0, y1 - y0);

            // The bearing is what a pen that sits on the baseline adds to reach the top-left corner of the glyph, so its
            // vertical part is negative above the baseline.
            glyphs[index] = new FontGlyph(default, sizes[index], new Vector2(x0, y0), advanceWidth * scale);
        }
    }

    /// <summary>Returns the characters as a sorted contiguous range.</summary>
    private static char[] Range(ReadOnlySpan<char> characters)
    {
        var wanted = new SortedSet<char>();

        foreach (char character in characters)
        {
            wanted.Add(character);
        }

        if (wanted.Count == 0)
        {
            throw new ArgumentException("At least one character has to be baked.", nameof(characters));
        }

        var range = new char[wanted.Count];
        wanted.CopyTo(range);

        for (int index = 1; index < range.Length; index++)
        {
            if (range[index] != range[index - 1] + 1)
            {
                throw new ArgumentException("The characters have to form a contiguous range, so that a glyph can be found by subtracting the first character.", nameof(characters));
            }
        }

        return range;
    }

    /// <summary>Rasterizes one glyph into the atlas as white coverage.</summary>
    private static unsafe void Draw(
        StbTrueType.stbtt_fontinfo info,
        int glyph,
        int glyphWidth,
        int glyphHeight,
        int targetX,
        int targetY,
        int atlasWidth,
        float scale,
        byte[] pixels)
    {
        if (glyphWidth <= 0 || glyphHeight <= 0)
        {
            return;
        }

        byte[] coverage = new byte[glyphWidth * glyphHeight];

        fixed (byte* pointer = coverage)
        {
            StbTrueType.stbtt_MakeGlyphBitmap(info, pointer, glyphWidth, glyphHeight, glyphWidth, scale, scale, glyph);
        }

        for (int row = 0; row < glyphHeight; row++)
        {
            int source = row * glyphWidth;

            for (int column = 0; column < glyphWidth; column++)
            {
                byte alpha = coverage[source + column];
                if (alpha == 0)
                {
                    continue;
                }

                int target = (((targetY + row) * atlasWidth) + targetX + column) * 4;
                pixels[target] = 255;
                pixels[target + 1] = 255;
                pixels[target + 2] = 255;
                pixels[target + 3] = alpha;
            }
        }
    }
}
