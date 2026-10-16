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

    /// <summary>Sets the handler that runs when the button is pressed.</summary>
    /// <param name="action">The handler, which the window service calls from the press event of the frame.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    /// <remarks>
    /// The handler is remembered by the builder and registered by the service when the window is opened, so the window
    /// that owns the button is the one that decides what a press does. A handler that closes the window it was built in
    /// is called at the boundary of the step, like every other event, and the window is gone by the next frame.
    /// </remarks>
    public ButtonHandle OnClick(Action action)
    {
        _builder.RegisterClick(Entity, action);
        return this;
    }
}
