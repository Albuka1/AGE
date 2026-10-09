using Age.Core;

namespace Age.Rendering;

/// <summary>
/// How a text is written: the fonts it may use, its color, and how its lines are fitted and placed in a box.
/// </summary>
/// <remarks>
/// <para>
/// A style is content: a document writes it, a scene saves it and a prototype inherits it, so a game changes how its text
/// looks without touching code. Everything about a text that is not the string and not the place it is drawn at lives here,
/// and the same style serves a line of a world and a label of an interface, which is what keeps the two from drifting
/// apart.
/// </para>
/// <para>
/// <see cref="Fonts"/> is a stack rather than one font: the first font that covers a character draws it, and a text that
/// leaves the stack empty or names a font that cannot be baked is drawn with the built-in bitmap font, which needs no file
/// and covers the printable ASCII range. Wrapping, alignment and overflow only matter for a text that is drawn into a box;
/// a box with no width or no height is as large as the text, so they leave a line of a world alone.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var style = new TextStyle
/// {
///     Fonts =
///     [
///         new FontStyle { Path = "Fonts/Cousine-Regular.ttf", PixelHeight = 24f, FirstCharacter = ' ', LastCharacter = '\u04FF' },
///     ],
///     Color = Color.White,
///     Align = TextAlign.Center,
///     Wrap = TextWrap.Word,
///     Overflow = TextOverflow.Ellipsis,
/// };
/// </code>
/// </example>
public struct TextStyle
{
    /// <summary>Gets or sets the fonts that the text may be drawn with, in the order they are tried.</summary>
    /// <remarks>An empty stack, or one whose fonts cover nothing of the text, is drawn with the built-in bitmap font.</remarks>
    public FontStyle[]? Fonts;

    /// <summary>Gets or sets the color of the glyphs.</summary>
    public Color Color;

    /// <summary>Gets or sets where a line is placed against the width of the box.</summary>
    public TextAlign Align;

    /// <summary>Gets or sets where the block of lines is placed against the height of the box.</summary>
    public TextVerticalAlign VerticalAlign;

    /// <summary>Gets or sets how a line that is wider than the box is broken.</summary>
    public TextWrap Wrap;

    /// <summary>Gets or sets what happens to the lines that do not fit the height of the box.</summary>
    public TextOverflow Overflow;

    /// <summary>Gets or sets the extra distance between two lines, in pixels, on top of the line height of the font.</summary>
    public float LineSpacing;

    /// <summary>Gets or sets the characters that end a line that was shortened because the text did not fit its box.</summary>
    /// <remarks>A leave of null means three dots, which every font of the built-in range covers.</remarks>
    public string? Ellipsis;
}
