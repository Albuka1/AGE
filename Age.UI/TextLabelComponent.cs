using Age.Core;

namespace Age.UI;

/// <summary>
/// Draws a text string on a UI element.
/// </summary>
[Component("TextLabel")]
public struct TextLabelComponent : IComponent
{
    /// <summary>Gets or sets the text to draw.</summary>
    public string? Text;

    /// <summary>Gets or sets the text color.</summary>
    public Color Color;
}
