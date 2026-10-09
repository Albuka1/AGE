namespace Age.Rendering;

/// <summary>
/// The fonts that the engine draws a text with when the text names none of its own.
/// </summary>
/// <remarks>
/// <para>
/// A game does not have to load a font to show a word: a text that names no font is drawn with the font the engine ships, so
/// Russian and English are readable out of the box, and a game that wants another font names it in the style of its text, which
/// is what a stack of fonts is for.
/// </para>
/// <para>
/// The font covers the Latin and Cyrillic letters in one range, and the punctuation a translation uses besides them in a second
/// entry of the same font: the em dash, the ellipsis and the quotation marks of a language lie outside the blocks that hold its
/// letters. A range that a font has no glyph for costs nothing but the walk over it, so one entry per block is cheaper than one
/// wide range.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// world.Set(label, new TextComponent { Key = "ent-Goblin" });                  // drawn with the font of the engine
///
/// world.Set(label, new TextComponent
/// {
///     Text = "Hello",
///     Style = new TextStyle { Fonts = [new FontStyle { Path = "Fonts/Own.ttf", PixelHeight = 32f }] },
/// });
/// </code>
/// </example>
public static class TextDefaults
{
    /// <summary>The path of the font the engine ships, relative to the game root.</summary>
    public const string FontPath = "Fonts/Cousine-Regular.ttf";

    /// <summary>The height of a line of the font the engine ships, in pixels.</summary>
    public const float PixelHeight = 16f;

    /// <summary>Gets the stack of fonts that a text which names no font of its own is drawn with.</summary>
    /// <remarks>
    /// The ranges are the printable ASCII range, the Cyrillic block and the punctuation above it, which is what a text of a
    /// game holds; a game that writes a third script adds an entry of its own.
    /// </remarks>
    public static FontStyle[] Fonts { get; } =
    [
        new FontStyle { Path = FontPath, PixelHeight = PixelHeight, FirstCharacter = ' ', LastCharacter = '\u04FF' },
        new FontStyle { Path = FontPath, PixelHeight = PixelHeight, FirstCharacter = '\u2000', LastCharacter = '\u22FF' },
    ];
}
