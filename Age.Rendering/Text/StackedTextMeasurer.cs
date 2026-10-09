using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Measures a text with a stack of fonts: the first font that covers a character is the one that measures it.
/// </summary>
/// <remarks>
/// <para>
/// A stack is what mixes the scripts of two languages in one line. A game that writes Russian puts the font of its game
/// first, which covers the Latin letters, and the font that covers the Cyrillic block after it; a game that writes Japanese
/// adds one that covers the block of its script. Every character is measured by the first font of the stack that covers it,
/// so a line that holds letters of two scripts is laid out as wide as the two of them.
/// </para>
/// <para>
/// A character that no font of the stack covers is measured by the first font, which draws it as a space, so a line keeps
/// the layout it was given rather than collapsing. The metrics are those of the first font as well, because that is the
/// font that a caller names first: the height of a line, the baseline and the place of an aligned line come from it, so a
/// stack whose fonts have different heights lays its lines out at the height of the first one.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var measurer = new StackedTextMeasurer(fonts,
/// [
///     (new FontStyle { Path = "Fonts/Cousine-Regular.ttf", PixelHeight = 24f }, latin),
///     (new FontStyle { Path = "Fonts/Cousine-Regular.ttf", PixelHeight = 24f, FirstCharacter = '\u0400', LastCharacter = '\u04FF' }, cyrillic),
/// ]);
/// </code>
/// </example>
public sealed class StackedTextMeasurer : ITextMeasurer
{
    private readonly IFontService _fonts;
    private readonly (FontStyle Style, FontHandle Font)[] _stack;

    /// <summary>Initializes the measurer with the service of the fonts and the stack of them, in the order they are tried.</summary>
    /// <param name="fonts">The service that baked the fonts.</param>
    /// <param name="stack">The styles of the stack with the handle of the font of each of them.</param>
    /// <exception cref="ArgumentNullException">The service or the stack is null.</exception>
    /// <exception cref="ArgumentException">The stack holds no font at all, because a text that names no font is measured with the built-in one.</exception>
    public StackedTextMeasurer(IFontService fonts, IReadOnlyList<(FontStyle Style, FontHandle Font)> stack)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(stack);

        if (stack.Count == 0)
        {
            throw new ArgumentException("A stack of fonts holds at least one font.", nameof(stack));
        }

        _fonts = fonts;
        _stack = [.. stack];
    }

    /// <inheritdoc />
    /// <remarks>The metrics are those of the first font of the stack, which is the font that a line of the text starts in.</remarks>
    public FontMetrics Metrics => _fonts.Metrics(_stack[0].Font);

    /// <inheritdoc />
    public Vector2 Measure(ReadOnlySpan<char> text)
    {
        float width = 0f;
        Span<char> one = stackalloc char[1];

        foreach (char character in text)
        {
            one[0] = character;
            width += _fonts.Measure(Font(character), one).X;
        }

        return new Vector2(width, Metrics.LineHeight);
    }

    /// <summary>Returns the font of the stack that covers a character, or the first one when no font does.</summary>
    /// <param name="value">The character to cover.</param>
    /// <returns>The handle of the font that draws the character.</returns>
    private FontHandle Font(char value)
    {
        foreach ((FontStyle style, FontHandle font) in _stack)
        {
            if (style.Covers(value))
            {
                return font;
            }
        }

        return _stack[0].Font;
    }
}
