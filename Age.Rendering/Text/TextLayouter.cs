using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Breaks a text into the lines that fit a box and places every line in it.
/// </summary>
/// <remarks>
/// <para>
/// The layout is pure: it measures through an <see cref="ITextMeasurer"/> and touches no device, no font file and no world,
/// so what a box of a given size does to a text is something a test pins down without a window. Everything a caller needs
/// to draw the text is in the result: the characters and the place of every line, and the size of the whole text.
/// </para>
/// <para>
/// A box of no width has no edge to break a line at and a box of no height has no bottom to cut a line off, so a text that
/// is laid out into such a box keeps the lines it was written with and takes the room it needs, which is what a line of a
/// world does. A line is broken where the text says so and then at a space; a word that is longer than the box is broken
/// wherever the box ends only when the style asks for it, because breaking a word in the middle is a choice.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ITextMeasurer measurer = new FontTextMeasurer(fonts, font);
/// TextLayout layout = TextLayouter.Layout(text, measurer, style, new Vector2(240f, 0f));
///
/// foreach (TextLine line in layout.Lines)
/// {
///     fonts.Draw(font, line.Text, position + line.Position, style.Color);
/// }
/// </code>
/// </example>
public static class TextLayouter
{
    /// <summary>Lays a text out into the lines that fit a box and places them in it.</summary>
    /// <param name="text">The text to lay out. A leave of null, or an empty text, has no line at all.</param>
    /// <param name="measurer">The measurement of the font that draws the text.</param>
    /// <param name="style">How the text is written: its alignment, its wrapping, its overflow and its spacing.</param>
    /// <param name="box">The width and the height of the box that holds the text. Zero means as large as the text.</param>
    /// <returns>The lines, the place of every line in the box, and the size of the text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measurer"/> is null.</exception>
    /// <remarks>
    /// A line feed ends a line and a carriage return before it is ignored, so a text that was written where lines end that
    /// way lays out the same as one that was written with line feeds alone. Every other character is a character of a line,
    /// including a tab, which is a character that the font draws or does not draw like any other.
    /// </remarks>
    public static TextLayout Layout(string? text, ITextMeasurer measurer, TextStyle style, Vector2 box)
    {
        ArgumentNullException.ThrowIfNull(measurer);

        if (string.IsNullOrEmpty(text))
        {
            return TextLayout.Empty;
        }

        float width = MathF.Max(box.X, 0f);
        float height = MathF.Max(box.Y, 0f);
        List<string> lines = Break(text, measurer, style, width);

        if (lines.Count == 0)
        {
            return TextLayout.Empty;
        }

        FontMetrics metrics = measurer.Metrics;
        float spacing = MathF.Max(style.LineSpacing, 0f);

        // A line takes the height of the font and the spacing after it, so the lines that fit are the ones whose bottom
        // stays inside the box. A box of no height is as tall as the text, so nothing is dropped.
        float step = metrics.LineHeight + spacing;
        int fits = lines.Count;

        if (height > 0f)
        {
            fits = Math.Clamp((int)MathF.Floor((height - metrics.LineHeight) / step) + 1, 0, lines.Count);
        }

        if (fits < lines.Count && fits > 0 && style.Overflow == TextOverflow.Ellipsis)
        {
            lines[fits - 1] = WithEllipsis(lines[fits - 1], style.Ellipsis ?? "...", measurer, width);
        }

        float block = fits == 0 ? 0f : ((fits - 1) * step) + metrics.LineHeight;
        float top = height > 0f
            ? style.VerticalAlign switch
            {
                TextVerticalAlign.Middle => (height - block) / 2f,
                TextVerticalAlign.Bottom => height - block,
                _ => 0f,
            }
            : 0f;

        var placed = new List<TextLine>(fits);
        float widest = 0f;

        for (var index = 0; index < fits; index++)
        {
            string line = lines[index];
            var size = new Vector2(measurer.Measure(line).X, metrics.LineHeight);
            float left = width > 0f
                ? style.Align switch
                {
                    TextAlign.Center => (width - size.X) / 2f,
                    TextAlign.End => width - size.X,
                    _ => 0f,
                }
                : 0f;

            placed.Add(new TextLine(line, size, new Vector2(left, top + (index * step))));
            widest = MathF.Max(widest, size.X);
        }

        return new TextLayout(placed, new Vector2(widest, block));
    }

    /// <summary>Breaks a text into the lines it was written with, and every one of those into the lines that fit the width.</summary>
    /// <param name="text">The text to break.</param>
    /// <param name="measurer">The measurement of the font.</param>
    /// <param name="style">How the text is written, which says whether a line is broken at all.</param>
    /// <param name="width">The width of the box, or zero when the text is as wide as it needs.</param>
    /// <returns>The lines of the text, in the order they are drawn.</returns>
    private static List<string> Break(string text, ITextMeasurer measurer, TextStyle style, float width)
    {
        var lines = new List<string>();
        int start = 0;

        for (var index = 0; index <= text.Length; index++)
        {
            if (index < text.Length && text[index] != '\n')
            {
                continue;
            }

            int end = index;

            // A carriage return before a line feed belongs to the break rather than to the text of the line.
            if (end > start && text[end - 1] == '\r')
            {
                end--;
            }

            Wrap(text[start..end], measurer, style.Wrap, width, lines);
            start = index + 1;
        }

        return lines;
    }

    /// <summary>Breaks one paragraph into the lines that fit the width and appends them.</summary>
    /// <param name="paragraph">The paragraph, which holds no line feed.</param>
    /// <param name="measurer">The measurement of the font.</param>
    /// <param name="wrap">How a line that is wider than the box is broken.</param>
    /// <param name="width">The width of the box, or zero when the text is as wide as it needs.</param>
    /// <param name="lines">The lines that are collected.</param>
    /// <remarks>
    /// The measurement walks the paragraph one character at a time, so a line is the longest text that still fits. A break
    /// happens at the last space of the line that is being built; a line that has no space in it is broken where it stands
    /// when the style allows it, and otherwise overflows, because a word that is cut in the middle is a decision of the
    /// caller rather than of the layout.
    /// </remarks>
    private static void Wrap(string paragraph, ITextMeasurer measurer, TextWrap wrap, float width, List<string> lines)
    {
        if (wrap == TextWrap.None || width <= 0f || paragraph.Length == 0)
        {
            lines.Add(paragraph);
            return;
        }

        int start = 0;
        int space = -1;

        for (var index = 0; index < paragraph.Length; index++)
        {
            if (paragraph[index] == ' ')
            {
                space = index;
            }

            if (measurer.Measure(paragraph.AsSpan(start, index - start + 1)).X <= width)
            {
                continue;
            }

            if (wrap == TextWrap.Anywhere)
            {
                lines.Add(paragraph[start..index]);
                start = index;
                space = -1;
                continue;
            }

            if (space > start)
            {
                lines.Add(paragraph[start..space]);
                start = space + 1;
                space = -1;
                index = start - 1;
            }
        }

        lines.Add(paragraph[start..]);
    }

    /// <summary>Shortens a line until the line and the ellipsis fit the width.</summary>
    /// <param name="line">The line to shorten.</param>
    /// <param name="ellipsis">The characters that mark the text that is not there.</param>
    /// <param name="measurer">The measurement of the font.</param>
    /// <param name="width">The width of the box, or zero when the text is as wide as it needs.</param>
    /// <returns>The shortened line.</returns>
    /// <remarks>
    /// The longest prefix that fits is kept, character by character from the end of the line, and the spaces before the
    /// ellipsis are dropped, so a shortened line does not end with a gap. A line that is too narrow for even the ellipsis
    /// ends with the ellipsis alone, which is what a caller sees instead of an empty box, and a text that has no width to
    /// fit into keeps its line and only says that more of it is not there.
    /// </remarks>
    private static string WithEllipsis(string line, string ellipsis, ITextMeasurer measurer, float width)
    {
        if (width <= 0f)
        {
            return string.Concat(line.AsSpan(), ellipsis.AsSpan());
        }

        for (var length = line.Length - 1; length >= 0; length--)
        {
            int end = length;

            while (end > 0 && line[end - 1] == ' ')
            {
                end--;
            }

            string candidate = string.Concat(line.AsSpan(0, end), ellipsis.AsSpan());

            if (measurer.Measure(candidate).X <= width)
            {
                return candidate;
            }
        }

        return ellipsis;
    }
}
