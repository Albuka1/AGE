using Age.Core;

namespace Age.UI;

/// <summary>
/// Positions and sizes a UI element in screen space.
/// </summary>
public struct RectTransformComponent : IComponent
{
    /// <summary>Gets or sets the position of the top-left corner, in screen coordinates.</summary>
    public Vector2 Position;

    /// <summary>Gets or sets the size.</summary>
    public Vector2 Size;

    /// <summary>Gets or sets the draw order. Larger values draw on top.</summary>
    public int ZOrder;

    /// <summary>Gets or sets a value indicating whether the element is drawn.</summary>
    public bool Visible;
}
