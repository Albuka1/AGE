using Age.Core;

namespace Age.Rendering;

/// <summary>
/// A text that is ready to draw: the lines that fit its box, the place of every line in it, and the size the text takes.
/// </summary>
/// <remarks>
/// A layout is built once and drawn on every frame: it holds no handle and no device object, so a game may keep one while
/// the text, the style and the box do not change, and build it again when one of them does, which is what a change of the
/// language of a game is. The size is what a box that follows its text needs, so a panel may grow to hold a line of a
/// title and a button may be as wide as the word on it.
/// </remarks>
/// <example>
/// <code>
/// TextLayout layout = TextLayouter.Layout(text, measurer, style, box);
///
/// foreach (TextLine line in layout.Lines)
/// {
///     fonts.Draw(font, line.Text, position + line.Position, style.Color);
/// }
/// </code>
/// </example>
public sealed class TextLayout
{
    /// <summary>Gets a layout that holds no line at all, which is what an empty text is laid out into.</summary>
    public static TextLayout Empty { get; } = new([], Vector2.Zero);

    /// <summary>Initializes a laid out text.</summary>
    /// <param name="lines">The lines of the text, in the order they are drawn.</param>
    /// <param name="size">The size of the text: the width of its widest line and the height of all of them.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is null.</exception>
    public TextLayout(IReadOnlyList<TextLine> lines, Vector2 size)
    {
        ArgumentNullException.ThrowIfNull(lines);
        Lines = lines;
        Size = size;
    }

    /// <summary>Gets the lines of the text, in the order they are drawn.</summary>
    public IReadOnlyList<TextLine> Lines { get; }

    /// <summary>Gets the size of the text: the width of its widest line and the height of all of them, in pixels.</summary>
    public Vector2 Size { get; }

    /// <summary>Gets a value indicating whether the layout holds no line at all.</summary>
    public bool IsEmpty => Lines.Count == 0;
}
