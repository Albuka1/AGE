using Age.Core;
using Age.UI;

namespace Age.Rendering;

/// <summary>
/// A rectangle of colour that holds other elements, which is what the frame of a window and the room of a list is.
/// </summary>
public sealed class PanelHandle : UiHandle<PanelHandle>
{
    internal PanelHandle(World world, Entity entity) : base(world, entity)
    {
    }

    /// <summary>Sets the colour the rectangle is filled with.</summary>
    /// <param name="color">The colour of the panel, whose alpha is what a window reads through it.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public PanelHandle Color(Color color)
    {
        World.GetRef<PanelComponent>(Entity).Color = color;
        return this;
    }
}
