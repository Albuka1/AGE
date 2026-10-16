using Age.Core;

namespace Age.UI;

/// <summary>
/// Makes a UI element react to pointer input.
/// </summary>
[Component("Button")]
public struct ButtonComponent : IComponent
{
    /// <summary>Gets or sets the color used when the button is idle.</summary>
    public Color BaseColor;

    /// <summary>Gets or sets a value indicating whether the pointer is currently over the button.</summary>
    public bool IsHovered;

    /// <summary>Gets or sets a value indicating whether the button is currently pressed.</summary>
    public bool IsPressed;

    /// <summary>Gets or sets a value indicating whether the button reacts to input.</summary>
    public bool Interactable;

    /// <summary>Gets or sets the colour of the frame drawn inside the rectangle of the button.</summary>
    /// <remarks>
    /// A transparent colour, which is the default of the field, draws no frame: a button that says nothing about its edge
    /// is a face of colour alone, and a game that wants a card, a slot or a tag writes the colour and the thickness of
    /// the frame here rather than placing a panel behind the button.
    /// </remarks>
    public Color BorderColor;

    /// <summary>Gets or sets how many pixels thick the frame is, where zero draws none.</summary>
    public int BorderThickness;
}
