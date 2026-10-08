using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Describes one glyph of a baked font: where it lives in the atlas and how it is placed on the baseline.
/// </summary>
internal readonly struct FontGlyph
{
    /// <summary>Initializes the description of a glyph.</summary>
    /// <param name="source">The region of the atlas that holds the glyph, in normalized coordinates.</param>
    /// <param name="size">The size of the glyph, in pixels.</param>
    /// <param name="bearing">The offset from the pen position to the top-left corner of the glyph, in pixels.</param>
    /// <param name="advance">The distance the pen moves after the glyph, in pixels.</param>
    public FontGlyph(Rect source, Vector2 size, Vector2 bearing, float advance)
    {
        Source = source;
        Size = size;
        Bearing = bearing;
        Advance = advance;
    }

    /// <summary>Gets the region of the atlas that holds the glyph, in normalized coordinates.</summary>
    public Rect Source { get; }

    /// <summary>Gets the size of the glyph, in pixels.</summary>
    public Vector2 Size { get; }

    /// <summary>Gets the offset from the pen position to the top-left corner of the glyph, in pixels.</summary>
    public Vector2 Bearing { get; }

    /// <summary>Gets the distance the pen moves after the glyph, in pixels.</summary>
    public float Advance { get; }
}
