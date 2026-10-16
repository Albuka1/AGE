using Age.Core;

namespace Age.UI;

/// <summary>
/// Draws an image inside the rectangle of the element, which is what an icon of an item or a picture of a panel is.
/// </summary>
/// <remarks>
/// The image is named by path rather than by a handle of the device, because a scene or a prototype that carries an
/// interface names a file the build ships rather than a texture of a run: the renderer resolves the path the first time
/// the element is drawn, and a path that no file answers is the placeholder of the texture service and one line in the
/// log.
/// </remarks>
[Component("Image")]
public struct ImageComponent : IComponent
{
    /// <summary>Gets or sets the path of the image, relative to the game root.</summary>
    [ResourcePath]
    public string? TexturePath;

    /// <summary>Gets or sets the tint the image is multiplied by, which is the colour of the element and not its shape.</summary>
    public Color Tint;
}
