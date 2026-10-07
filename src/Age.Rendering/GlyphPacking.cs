using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Places the boxes of a font into an atlas whose width is a power of two.
/// </summary>
/// <remarks>
/// The boxes are placed left to right in shelves and a new shelf starts when the next box does not fit, which is the
/// simplest layout that keeps the work of baking linear and the result deterministic. The width is doubled until the
/// boxes fit in a square or a landscape rectangle, which leaves the atlas close to the area the glyphs need instead of
/// one row per glyph.
/// </remarks>
internal static class GlyphPacking
{
    /// <summary>The margin that is left around every box, in pixels, so a sample cannot reach the neighbour of a glyph.</summary>
    public const int Padding = 1;

    /// <summary>The smallest width of an atlas, in pixels.</summary>
    public const int MinimumWidth = 64;

    /// <summary>Places the boxes and reports the size of the atlas that holds them.</summary>
    /// <param name="sizes">The size of every box, in pixels, in the order the caller wants them placed.</param>
    /// <param name="placements">The span that receives the top-left corner of every box, in the same order.</param>
    /// <param name="width">The width of the atlas, in pixels.</param>
    /// <param name="height">The height of the atlas, in pixels.</param>
    /// <exception cref="ArgumentException"><paramref name="placements"/> is shorter than <paramref name="sizes"/>.</exception>
    public static void Place(ReadOnlySpan<Vector2> sizes, Span<(int X, int Y)> placements, out int width, out int height)
    {
        if (placements.Length < sizes.Length)
        {
            throw new ArgumentException("The span for the placements is shorter than the list of sizes.", nameof(placements));
        }

        width = StartWidth(sizes);

        while (!TryPlace(sizes, placements, width, out height))
        {
            width *= 2;
        }
    }

    /// <summary>Returns the width to start with: the largest box in a power of two that is not smaller than the minimum.</summary>
    private static int StartWidth(ReadOnlySpan<Vector2> sizes)
    {
        int largest = 0;

        foreach (Vector2 size in sizes)
        {
            largest = Math.Max(largest, (int)MathF.Ceiling(MathF.Max(size.X, size.Y)) + (2 * Padding));
        }

        int width = MinimumWidth;
        while (width < largest)
        {
            width *= 2;
        }

        return width;
    }

    /// <summary>Places the boxes in an atlas of the given width. Reports <see langword="false"/> when they do not fit in a square.</summary>
    private static bool TryPlace(ReadOnlySpan<Vector2> sizes, Span<(int X, int Y)> placements, int width, out int height)
    {
        int cursorX = Padding;
        int cursorY = Padding;
        int shelfHeight = 0;
        height = 2 * Padding;

        for (int index = 0; index < sizes.Length; index++)
        {
            int cellWidth = (int)MathF.Ceiling(sizes[index].X) + (2 * Padding);
            int cellHeight = (int)MathF.Ceiling(sizes[index].Y) + (2 * Padding);

            if (cursorX + cellWidth > width)
            {
                cursorX = Padding;
                cursorY += shelfHeight;
                shelfHeight = 0;
            }

            placements[index] = (cursorX, cursorY);
            cursorX += cellWidth;
            shelfHeight = Math.Max(shelfHeight, cellHeight);
            height = cursorY + shelfHeight + Padding;
        }

        return height <= width;
    }
}
