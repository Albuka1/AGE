using System.Text.Json.Serialization;
using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Draws a text where its entity stands, or in the box of its rectangle when it is a label of the interface.
/// </summary>
/// <remarks>
/// <para>
/// One component serves a line of a world and a label of an interface, because the difference between them is only where the
/// text stands: an entity with a <see cref="TransformComponent"/> has its line at the position of the transform, and one
/// with a rectangle of the interface has its label in that rectangle, which is also the box the text is laid out into. That
/// is what keeps a key of the strings of a game, a stack of fonts, a box that cuts a text off and a font that cannot be
/// baked behaving the same in both places.
/// </para>
/// <para>
/// The string is content in either case: <see cref="Key"/> names what the text says in the language that is being played,
/// with <see cref="Count"/> written into it as the count of its plural form, and <see cref="Text"/> is what a game that has
/// no key for its line writes. A key wins over a text, so a line that gains a key says what the language says.
/// </para>
/// <para>
/// What the renderer resolves is written back for a game to read: <see cref="Font"/> is the font that draws the line, and
/// <see cref="MeasuredSize"/> is the size the text takes, which is what a panel that follows its title or a button that is
/// as wide as its word asks for.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// world.Set(label, new TextComponent
/// {
///     Key = "ent-Goblin",
///     Style = new TextStyle { Fonts = [font], Align = TextAlign.Center, Overflow = TextOverflow.Ellipsis },
///     ZOrder = 10,
/// });
/// </code>
/// </example>
[Component("Text")]
public struct TextComponent : IComponent
{
    /// <summary>Gets or sets what the text says, which is drawn when the text names no key.</summary>
    public string? Text;

    /// <summary>Gets or sets the key of the strings of a game, which wins over <see cref="Text"/>.</summary>
    /// <remarks>
    /// A key is answered in the language that is being played, so a line that switches language says the new one as soon as
    /// the language of the game does, without a game writing the text of it again.
    /// </remarks>
    public string? Key;

    /// <summary>Gets or sets the count that the key is written by, which is the argument named <c>count</c> of its text.</summary>
    /// <remarks>A text that writes its string by count has no text of its own, so a key of that kind is drawn as a count of things.</remarks>
    public int Count;

    /// <summary>Gets or sets the box that the text is laid out into, where zero means as large as the text.</summary>
    /// <remarks>
    /// A label of the interface of an entity without a rectangle is laid out into this box, and a client may set it to limit
    /// a line of a world that is wider than it should be. A box of no width has no edge to break a line at, and one of no
    /// height has no bottom to cut a line off.
    /// </remarks>
    public Vector2 Box;

    /// <summary>Gets or sets how the text is written: its fonts, its color and how its lines fit a box.</summary>
    public TextStyle Style;

    /// <summary>Gets or sets the draw order. Larger values draw on top.</summary>
    public int ZOrder;

    /// <summary>Gets the font that draws the text.</summary>
    /// <remarks>
    /// The renderer writes it after it bakes the stack, and a game reads it rather than sets it: the handle of a device
    /// texture is state of a run, so it is not part of what a scene or a prototype carries.
    /// </remarks>
    [JsonIgnore]
    public FontHandle Font;

    /// <summary>Gets the size that the text takes, in pixels, which the renderer writes.</summary>
    /// <remarks>The size is what a box that follows its text needs: a panel that grows to its title, a button as wide as the word on it.</remarks>
    [JsonIgnore]
    public Vector2 MeasuredSize;
}

