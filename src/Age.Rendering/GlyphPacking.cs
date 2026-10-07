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

    /// <summary>The largest width or height of an atlas, and of the cell of a single glyph, in pixels.</summary>
    /// <remarks>The bound keeps the search for a width finite and keeps every dimension of the atlas within a range that the arithmetic below cannot overflow.</remarks>
    public const int MaximumDimension = 8192;

    /// <summary>The largest number of pixels of an atlas, which bounds the memory that baking one font can need.</summary>
    public const int MaximumPixels = 16 * 1024 * 1024;

    /// <summary>Places the boxes and reports the size of the atlas that holds them.</summary>
    /// <param name="sizes">The size of every box, in pixels, in the order the caller wants them placed.</param>
    /// <param name="placements">The span that receives the top-left corner of every box, in the same order.</param>
    /// <param name="width">The width of the atlas, in pixels.</param>
    /// <param name="height">The height of the atlas, in pixels.</param>
    /// <exception cref="ArgumentException"><paramref name="placements"/> is shorter than <paramref name="sizes"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A box does not have a finite, non-negative size that fits in a cell, or the boxes need an atlas of more than the largest supported size.</exception>
    public static void Place(ReadOnlySpan<Vector2> sizes, Span<(int X, int Y)> placements, out int width, out int height)
    {
        if (placements.Length < sizes.Length)
        {
            throw new ArgumentException("The span for the placements is shorter than the list of sizes.", nameof(placements));
        }

        width = StartWidth(sizes);

        while (true)
        {
            if (TryPlace(sizes, placements, width, out height))
            {
                // The width is bounded, so the area cannot overflow a long, and the caller is told before it allocates.
                if ((long)width * height > MaximumPixels)
                {
                    throw new ArgumentOutOfRangeException(nameof(sizes), $"The boxes need an atlas of {width} by {height} pixels, which is more than the {MaximumPixels} pixels that are supported.");
                }

                return;
            }

            if (width >= MaximumDimension)
            {
                throw new ArgumentOutOfRangeException(nameof(sizes), $"The boxes do not fit in an atlas of at most {MaximumDimension} by {MaximumDimension} pixels.");
            }

            width = Math.Min(width * 2, MaximumDimension);
        }
    }

    /// <summary>Returns the width to start with: the largest cell in a power of two that is not smaller than the minimum.</summary>
    private static int StartWidth(ReadOnlySpan<Vector2> sizes)
    {
        int largest = 0;

        foreach (Vector2 size in sizes)
        {
            largest = Math.Max(largest, CellSize(size));
        }

        int width = MinimumWidth;
        while (width < largest)
        {
            width = Math.Min(width * 2, MaximumDimension);
        }

        return width;
    }

    /// <summary>Returns the size of the cell of a box: its larger side rounded up, with the padding of both sides.</summary>
    /// <remarks>A box whose larger side is zero or negative is an empty glyph: it gets a cell for the padding alone, which the rasterizer then skips.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The larger side of the box is not a finite, non-negative size, or its cell would be larger than <see cref="MaximumDimension"/>.</exception>
    private static int CellSize(Vector2 size)
    {
        float largest = MathF.Max(size.X, size.Y);

        if (!float.IsFinite(largest) || largest < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "A glyph box has to be a finite, non-negative size.");
        }

        int cell = (int)MathF.Ceiling(largest) + (2 * Padding);
        if (cell > MaximumDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, $"A glyph box may not need a cell of more than {MaximumDimension} pixels.");
        }

        return cell;
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
