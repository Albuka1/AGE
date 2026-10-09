using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Measures a text with the font that draws it, which is what the layout of a text is built on.
/// </summary>
/// <remarks>
/// This is the seam that keeps the layout of a text free of the graphics device and of the font service: a layout asks how
/// wide a run of characters is and how tall a line of the font is, and it never learns whether the answer comes from one
/// baked font, from a stack of them, or from the built-in bitmap font. A character that no font of the measurer covers
/// counts as a space, which is what the font service does, so a line keeps the layout it was given.
/// </remarks>
/// <example>
/// <code>
/// ITextMeasurer measurer = new FontTextMeasurer(fonts, font);
/// TextLayout layout = TextLayouter.Layout("Hello AGE", measurer, style, new Vector2(240f, 0f));
/// </code>
/// </example>
public interface ITextMeasurer
{
    /// <summary>Gets the metrics that place a line of the text: its ascent and the distance between two baselines.</summary>
    FontMetrics Metrics { get; }

    /// <summary>Measures a run of characters of the text.</summary>
    /// <param name="text">The characters to measure.</param>
    /// <returns>The width that the characters advance and the height of a line, in pixels.</returns>
    Vector2 Measure(ReadOnlySpan<char> text);

    /// <summary>Returns the font that draws a character of the text.</summary>
    /// <param name="value">The character to look up.</param>
    /// <returns>The handle of the font that draws the character, or null when the built-in bitmap font draws it.</returns>
    /// <remarks>
    /// A caller that draws a line asks this for every character of it, so a line that mixes the scripts of two languages is
    /// drawn with the font of each of them. The advance of the character comes from <see cref="Measure"/> either way, so
    /// what is drawn and what was measured agree.
    /// </remarks>
    FontHandle? Font(char value);
}
