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
        ref TextComponent label = ref World.GetRef<TextComponent>(Entity);
        label.Text = text;
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
}
