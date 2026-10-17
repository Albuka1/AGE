using Age.Core;

namespace Age.Rendering;

/// <summary>
/// A line of text of a window, which is what a title, a caption and the name of a button is.
/// </summary>
public sealed class LabelHandle : UiHandle<LabelHandle>
{
    internal LabelHandle(World world, Entity entity) : base(world, entity)
    {
    }

    /// <summary>Sets the words the label draws.</summary>
    /// <param name="text">The line, which a document may also write as a key of a language.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    /// <remarks>
    /// A label names its words rather than a key: a window is a class of a game and its strings are usually written where
    /// it draws them. A label that a translation has to answer is built with a key instead, which is what
    /// <see cref="Key"/> is for.
    /// </remarks>
    public LabelHandle Text(string text)
    {
        World.GetRef<TextComponent>(Entity).Text = text;
        return this;
    }

    /// <summary>Sets the key of the strings of a game that the label draws, which is what a translated caption is.</summary>
    /// <param name="key">The key of the strings of the game, such as <c>ui-inventory</c>.</param>
    /// <param name="count">The count that the key is written by, which is what a text of a plural form reads.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public LabelHandle Key(string key, int count = 0)
    {
        ref TextComponent label = ref World.GetRef<TextComponent>(Entity);
        label.Key = key;
        label.Count = count;
        return this;
    }

    /// <summary>Sets the colour of the glyphs.</summary>
    /// <param name="color">The colour of the line.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public LabelHandle Color(Color color)
    {
        World.GetRef<TextComponent>(Entity).Style.Color = color;
        return this;
    }

    /// <summary>Sets where the line is placed across the box that holds it.</summary>
    /// <param name="align">The alignment of the line.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public LabelHandle Align(TextAlign align)
    {
        World.GetRef<TextComponent>(Entity).Style.Align = align;
        return this;
    }

    /// <summary>Sets where the block of lines is placed against the height of the box that holds it.</summary>
    /// <param name="align">The vertical alignment of the block of lines.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public LabelHandle VerticalAlign(TextVerticalAlign align)
    {
        World.GetRef<TextComponent>(Entity).Style.VerticalAlign = align;
        return this;
    }

    /// <summary>Sets how a line that is wider than the box is broken.</summary>
    /// <param name="wrap">How a line that does not fit is broken.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public LabelHandle Wrap(TextWrap wrap)
    {
        World.GetRef<TextComponent>(Entity).Style.Wrap = wrap;
        return this;
    }

    /// <summary>Sets what happens to the lines that do not fit the height of the box.</summary>
    /// <param name="overflow">What is done with the lines that do not fit.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public LabelHandle Overflow(TextOverflow overflow)
    {
        World.GetRef<TextComponent>(Entity).Style.Overflow = overflow;
        return this;
    }

    /// <summary>Sets the extra distance between two lines, on top of the line height of the font.</summary>
    /// <param name="spacing">The distance between two lines, in pixels.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public LabelHandle LineSpacing(float spacing)
    {
        World.GetRef<TextComponent>(Entity).Style.LineSpacing = spacing;
        return this;
    }

    /// <summary>Sets the height of a line of the label, which is what tells a title from a caption.</summary>
    /// <param name="pixelHeight">The height of a line, in pixels of the design area.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    /// <remarks>
    /// The font is the one the engine ships, which covers the Latin and Cyrillic letters and the punctuation a translation
    /// uses. A game that ships a font of its own names it with the overload that takes a path. The height is what a bake
    /// is made at, so a line of twenty-four pixels and one of twelve are two atlases and not one scaled.
    /// </remarks>
    public LabelHandle Font(float pixelHeight) => Font(TextDefaults.FontPath, pixelHeight);

    /// <summary>Sets the font of the label and the height of a line of it.</summary>
    /// <param name="path">The path of the font file, relative to the game root.</param>
    /// <param name="pixelHeight">The height of a line, in pixels of the design area.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public LabelHandle Font(string path, float pixelHeight)
    {
        World.GetRef<TextComponent>(Entity).Style.Fonts =
        [
            new FontStyle { Path = path, PixelHeight = pixelHeight },
        ];

        return this;
    }

    /// <summary>Writes the whole style of the label, which is what a caller that wants a stack of fonts or an ellipsis of its own uses.</summary>
    /// <param name="style">The style to write.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    /// <remarks>
    /// The style is written as it stands, and the label reads the fonts of the style on every frame it is laid out: the
    /// two shorthands above are the common cases of a stack of one font, and this is the seam a game reaches for the rest.
    /// </remarks>
    public LabelHandle Style(TextStyle style)
    {
        World.GetRef<TextComponent>(Entity).Style = style;
        return this;
    }
}
