using Age.Core;

namespace Age.UI;

/// <summary>
/// Makes a UI element react to pointer input.
/// </summary>
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
}
