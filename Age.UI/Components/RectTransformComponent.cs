using Age.Core;

namespace Age.UI;

/// <summary>
/// Positions and sizes a UI element in screen space, in pixels, with the origin at the top-left corner of the window.
/// </summary>
/// <remarks>
/// Screen space means the element is placed independently of the world camera and the y axis grows downwards. Point
/// tests such as the ones in <see cref="UIUpdateSystem"/> use the same coordinates, so a pointer position can be
/// compared with <see cref="Position"/> directly.
/// </remarks>
[Component("RectTransform")]
public struct RectTransformComponent : IComponent
{
    /// <summary>Gets or sets the position of the top-left corner of the element, in screen pixels.</summary>
    public Vector2 Position;

    /// <summary>Gets or sets the size.</summary>
    public Vector2 Size;

    /// <summary>Gets or sets the draw order. Larger values draw on top.</summary>
    public int ZOrder;

    /// <summary>Gets or sets a value indicating whether the element is drawn.</summary>
    public bool Visible;
}
