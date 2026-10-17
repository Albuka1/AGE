using Age.Core;

namespace Age.UI;

/// <summary>
/// Draws an image inside the rectangle of the element, which is what an icon of an item or a picture of a panel is.
/// </summary>
/// <remarks>
/// <para>
/// The image is named by path rather than by a handle of the device, because a scene or a prototype that carries an
/// interface names a file the build ships rather than a texture of a run: the renderer resolves the path the first time
/// the element is drawn, and a path that no file answers is the placeholder of the texture service and one line in the
/// log.
/// </para>
/// <para>
/// The tint is nullable rather than a colour whose alpha of zero is read as the default of the structure: a document
/// that names no tint leaves the field at null and the image is drawn as the artist drew it, while a document that names
/// a tint — including one whose alpha is zero — is drawn with the colour it asked for. A colour of no alpha is therefore
/// a colour a designer asked for rather than a colour that stands for the absence of one.
/// </para>
/// </remarks>
[Component("Image")]
public struct ImageComponent : IComponent
{
    /// <summary>Gets or sets the path of the image, relative to the game root.</summary>
    [ResourcePath]
    public string? TexturePath;

    /// <summary>Gets or sets the tint the image is multiplied by, which is the colour of the element and not its shape, or null to draw the image as it is.</summary>
    /// <remarks>
    /// A document that leaves this field alone reads it as null and the image is drawn white, which is the image as the
    /// artist drew it. A document that writes a colour reads that colour, whether its alpha is zero or not, so an image
    /// that is drawn transparently is an image a designer can ask for rather than one that is read as an image that named
    /// no colour at all.
    /// </remarks>
    public Color? Tint;
}
