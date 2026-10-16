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

    /// <summary>Sets the colour of the frame drawn inside the rectangle, and how thick it is.</summary>
    /// <param name="color">The colour of the frame, where a transparent colour draws none.</param>
    /// <param name="thickness">How many pixels thick the frame is, where zero draws none. The default is one.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    /// <remarks>
    /// The frame is what a panel says about its edge: a card, a slot and a window are drawn by the frame alone, and the
    /// element that draws it keeps the rectangle it was given, because the frame is drawn inside rather than beside it.
    /// </remarks>
    public PanelHandle Border(Color color, int thickness = 1)
    {
        ref PanelComponent panel = ref World.GetRef<PanelComponent>(Entity);
        panel.BorderColor = color;
        panel.BorderThickness = thickness;
        return this;
    }
}
