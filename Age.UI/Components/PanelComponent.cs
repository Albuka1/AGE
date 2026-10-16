using Age.Core;

namespace Age.UI;

/// <summary>
/// Draws a filled rectangle where the element stands, which is the background of a panel and of a window.
/// </summary>
/// <remarks>
/// <para>
/// A panel is what an element is drawn on rather than what a person clicks: a <see cref="ButtonComponent"/> answers the
/// pointer, a panel only fills the room of its rectangle. Two of them on one element are the same panel, and an element
/// that carries a panel and a button is drawn with the colour of the panel and reacts to the pointer of the button.
/// </para>
/// <para>
/// The component is the shape of a background rather than a sprite: what is drawn is the colour alone, and a rectangle
/// that is not there is one whose alpha is zero.
/// </para>
/// </remarks>
[Component("Panel")]
public struct PanelComponent : IComponent
{
    /// <summary>Gets or sets the colour the rectangle is filled with.</summary>
    public Color Color;
}
