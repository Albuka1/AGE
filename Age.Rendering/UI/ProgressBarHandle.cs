using Age.Core;
using Age.UI;

namespace Age.Rendering;

/// <summary>
/// A bar that reports how far a value has come, which is what the points and the armor of a character are.
/// </summary>
public sealed class ProgressBarHandle : UiHandle<ProgressBarHandle>
{
    internal ProgressBarHandle(World world, Entity entity) : base(world, entity)
    {
    }

    /// <summary>Sets the colour the filled part of the bar is drawn in.</summary>
    /// <param name="color">The colour of the fill.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    /// <remarks>
    /// The colour is written into the panel of the element rather than into the bar: a bar of the engine draws a track of
    /// its own and a fill that a game gives it. A bar that names no colour is drawn with the colour the builder gives it,
    /// which is a green of points, and a window that writes another one changes the fill alone.
    /// </remarks>
    public ProgressBarHandle Color(Color color)
    {
        World.GetRef<PanelComponent>(Entity).Color = color;
        return this;
    }
}
