using System.Text.Json.Serialization;
using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Draws one line of text in the world with the font that <see cref="FontPath"/> names.
/// </summary>
/// <remarks>
/// <para>
/// A game asks the locale service for the string of a key and puts the answer here, so what a player reads is content rather
/// than code and a new language is a folder of documents. A line is placed by the transform of its entity, which puts it in
/// the world next to the sprites, and ordered by <see cref="ZOrder"/> among the other lines.
/// </para>
/// <para>
/// The font is baked for the range from <see cref="FirstCharacter"/> to <see cref="LastCharacter"/>, which is what a language
/// with a script of its own needs: a game that writes Russian asks for the space to the end of the Cyrillic block, because a
/// character outside the range of a font is drawn as a space. A leave of zero means the printable ASCII range, which is the
/// range that the built-in font of the renderer covers.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// world.Set(label, new TransformComponent { Position = new Vector2(400f, 260f) });
/// world.Set(label, new TextComponent
/// {
///     Text = locale.Get(goblin.NameKey),
///     FontPath = "Fonts/Cousine-Regular.ttf",
///     PixelHeight = 24f,
///     FirstCharacter = ' ',
///     LastCharacter = '\u04FF',
///     Color = Color.White,
///     ZOrder = 10,
/// });
/// </code>
/// </example>
[Component("Text")]
public struct TextComponent : IComponent
{
    /// <summary>Gets or sets the text of the line.</summary>
    public string? Text;

    /// <summary>Gets or sets the path of the font file, relative to the game root.</summary>
    /// <remarks>
    /// The field content writes, because a handle belongs to the graphics device and a device is not saved.
    /// <c>Age.Content.Lint</c> checks the path against the files of a build, so a font that is not there fails a build
    /// rather than reaching a frame, and a font that cannot be baked at run time is reported once and drawn with the
    /// built-in font of the renderer.
    /// </remarks>
    [ResourcePath]
    public string? FontPath;

    /// <summary>Gets or sets the height of a line, in pixels. A line of a height of zero or less is not drawn.</summary>
    public float PixelHeight;

    /// <summary>Gets or sets the color of the glyphs.</summary>
    public Color Color;

    /// <summary>Gets or sets the draw order. Larger values draw on top.</summary>
    public int ZOrder;

    /// <summary>Gets or sets the first character of the range that the font is baked for. Zero means the space.</summary>
    public char FirstCharacter;

    /// <summary>Gets or sets the last character of the range that the font is baked for. Zero means the tilde, which is the printable ASCII range.</summary>
    /// <remarks>Such as <c>'\u04FF'</c> for the end of the Cyrillic block, which is what the font of this repository covers.</remarks>
    public char LastCharacter;

    /// <summary>Gets the font that was baked for this line.</summary>
    /// <remarks>
    /// This is the handle of a font that the service loaded, so it is state of a run rather than data of content and is not
    /// part of what a scene or a prototype carries: the render system writes it after it bakes the font, and a game reads it
    /// rather than sets it.
    /// </remarks>
    [JsonIgnore]
    public FontHandle Font;
}
