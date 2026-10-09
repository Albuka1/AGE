using System.Text.Json.Serialization;
using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Draws the image that <see cref="TexturePath"/> names, a handle that a game set, or a solid color quad when neither is set.
/// </summary>
/// <remarks>
/// A sprite that content describes names its image by path, which is the one thing about a sprite that survives a save:
/// the handle belongs to the graphics device, and a device is not saved. The path is resolved the first time the sprite is
/// drawn, and an image that is not there becomes the placeholder of the texture service rather than a failed frame.
/// </remarks>
[Component("Sprite")]
public struct SpriteComponent : IComponent
{
    /// <summary>Gets or sets the texture. A zero identifier renders a solid color quad.</summary>
    /// <remarks>
    /// This is the handle of a texture that a game loaded itself, and it is not written to a scene, which is why a sprite
    /// that content describes names its image with <see cref="TexturePath"/> instead.
    /// </remarks>
    [JsonIgnore]
    public TextureHandle Texture;

    /// <summary>Gets or sets the path of the image, relative to the game root.</summary>
    /// <remarks>
    /// The field a document writes, so a prototype or a scene names a sprite the way a game loads an asset. An image that
    /// is not there is drawn as the placeholder of the texture service and reported once, which makes a mistake in the
    /// content of a game visible rather than fatal.
    /// </remarks>
    public string? TexturePath;

    /// <summary>Gets or sets the base size, in pixels, before the transform scale is applied.</summary>
    public Vector2 Size;

    /// <summary>Gets or sets the tint color.</summary>
    public Color Color;

    /// <summary>Gets or sets the draw order. Larger values draw on top.</summary>
    public int ZOrder;
}
