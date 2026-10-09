using Age.Core;

namespace Age.UI;

/// <summary>
/// Draws a text string on a UI element.
/// </summary>
/// <remarks>
/// <para>
/// A label draws with the built-in 8x8 bitmap font unless it names a font of its own, which is what a language whose script
/// the bitmap font does not cover needs: <see cref="FontPath"/> names the file, <see cref="PixelHeight"/> the height of a
/// line, and the range from <see cref="FirstCharacter"/> to <see cref="LastCharacter"/> what the font is baked for.
/// </para>
/// <para>
/// The text of a label is what a game asks the locale service for, so a label says what the language of the game says rather
/// than what the code of the game holds.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// world.Set(panel, new TextLabelComponent
/// {
///     Text = locale.Get("ui-entities", ("count", 3)),
///     FontPath = "Fonts/Cousine-Regular.ttf",
///     PixelHeight = 16f,
///     LastCharacter = '\u04FF',
///     Color = Color.White,
/// });
/// </code>
/// </example>
[Component("TextLabel")]
public struct TextLabelComponent : IComponent
{
    /// <summary>Gets or sets the text to draw.</summary>
    public string? Text;

    /// <summary>Gets or sets the text color.</summary>
    public Color Color;

    /// <summary>Gets or sets the path of the font to draw with, relative to the game root.</summary>
    /// <remarks>
    /// A label that leaves this unset draws with the built-in font of the renderer, which is what a game that ships no font
    /// uses. The field is content, so <c>Age.Content.Lint</c> checks the path against the files of a build and a font that
    /// cannot be baked is drawn with the built-in font and reported once rather than taking a frame down.
    /// </remarks>
    [ResourcePath]
    public string? FontPath;

    /// <summary>Gets or sets the height of a line, in pixels. A label that names a font has to say it, and one of zero or less draws with the built-in font.</summary>
    public float PixelHeight;

    /// <summary>Gets or sets the first character of the range that the font is baked for. Zero means the space.</summary>
    public char FirstCharacter;

    /// <summary>Gets or sets the last character of the range that the font is baked for. Zero means the tilde, which is the printable ASCII range.</summary>
    /// <remarks>Such as <c>'\u04FF'</c> for the end of the Cyrillic block, which is what the font of this repository covers.</remarks>
    public char LastCharacter;
}
