using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Draws a texture, or a solid color quad when no texture is set.
/// </summary>
[Component("Sprite")]
public struct SpriteComponent : IComponent
{
    /// <summary>Gets or sets the texture. A zero identifier renders a solid color quad.</summary>
    public TextureHandle Texture;

    /// <summary>Gets or sets the base size, in pixels, before the transform scale is applied.</summary>
    public Vector2 Size;

    /// <summary>Gets or sets the tint color.</summary>
    public Color Color;

    /// <summary>Gets or sets the draw order. Larger values draw on top.</summary>
    public int ZOrder;
}
