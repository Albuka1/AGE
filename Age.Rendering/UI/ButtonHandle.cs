using Age.Core;
using Age.UI;

namespace Age.Rendering;

/// <summary>
/// A rectangle that answers the pointer, which is what a button of a window is.
/// </summary>
public sealed class ButtonHandle : UiHandle<ButtonHandle>
{
    private readonly UiBuilder _builder;

    internal ButtonHandle(UiBuilder builder, World world, Entity entity) : base(world, entity) => _builder = builder;

    /// <summary>Sets the words the button draws over its face.</summary>
    /// <param name="text">The caption of the button.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    /// <remarks>
    /// The caption is a component of the button itself rather than a label beside it, because a label over a button is
    /// a second element under the pointer: a press that lands on the words of a button has to be a press on the button,
    /// and an element of its own would stand in the way of it.
    /// </remarks>
    public ButtonHandle Text(string text)
    {
        if (World.Has<TextComponent>(Entity))
        {
            World.GetRef<TextComponent>(Entity).Text = text;
        }

        return this;
    }

    /// <summary>Sets the colour of the face of the button.</summary>
    /// <param name="color">The colour of the face, which the renderer lightens when the pointer is over it.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public ButtonHandle Color(Color color)
    {
        World.GetRef<ButtonComponent>(Entity).BaseColor = color;
        return this;
    }

    /// <summary>Sets the colour of the words of the button.</summary>
    /// <param name="color">The colour of the caption.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public ButtonHandle TextColor(Color color)
    {
        if (World.Has<TextComponent>(Entity))
        {
            World.GetRef<TextComponent>(Entity).Style.Color = color;
        }

        return this;
    }

    /// <summary>Sets the height of the line of the caption, which is what tells a title of a button from its label.</summary>
    /// <param name="pixelHeight">The height of a line, in pixels of the design area.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public ButtonHandle Font(float pixelHeight) => Font(TextDefaults.FontPath, pixelHeight);

    /// <summary>Sets the font of the caption and the height of a line of it.</summary>
    /// <param name="path">The path of the font file, relative to the game root.</param>
    /// <param name="pixelHeight">The height of a line, in pixels of the design area.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public ButtonHandle Font(string path, float pixelHeight)
    {
        if (World.Has<TextComponent>(Entity))
        {
            World.GetRef<TextComponent>(Entity).Style.Fonts =
            [
                new FontStyle { Path = path, PixelHeight = pixelHeight },
            ];
        }

        return this;
    }

    /// <summary>Sets the colour of the frame drawn inside the rectangle of the button, and how thick it is.</summary>
    /// <param name="color">The colour of the frame, where a transparent colour draws none.</param>
    /// <param name="thickness">How many pixels thick the frame is, where zero draws none. The default is one.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    /// <remarks>
    /// The frame is what a button says about its edge: a card, a slot of a queue and a tag are drawn by the frame alone,
    /// and a button that writes one does not need a panel behind it to stand out from the room it is drawn in.
    /// </remarks>
    public ButtonHandle Border(Color color, int thickness = 1)
    {
        ref ButtonComponent button = ref World.GetRef<ButtonComponent>(Entity);
        button.BorderColor = color;
        button.BorderThickness = thickness;
        return this;
    }

    /// <summary>Sets the handler that runs when the button is pressed.</summary>
    /// <param name="action">The handler, which the window service calls from the press event of the frame.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public ButtonHandle OnClick(Action action)
    {
        _builder.RegisterClick(Entity, action);
        return this;
    }
}
