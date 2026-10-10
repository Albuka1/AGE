using Age.Core;

namespace Age.Rendering;

/// <summary>
/// One layer of a <see cref="SpriteComponent"/>: the image it draws and the shader that draws it.
/// </summary>
/// <remarks>
/// <para>
/// A layer is what a sprite is made of when one image is not enough: the body of a character, the clothes over it and the glow
/// that breathes are the layers of one entity, each with an image and a shader of its own, rather than three entities that
/// follow one another. The layers of a sprite are drawn in the order the document writes them, so the first is the one at the
/// bottom, and every layer is drawn at the position, the size and the colour of the sprite.
/// </para>
/// <para>
/// A layer that names no shader is drawn with the program of the engine, which is what an unshaded layer is: the tint of the
/// sprite multiplies the image and nothing else happens to it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// - type: Sprite
///   Size:
///     X: 40
///     Y: 40
///   Layers:
///     - Name: base
///       Image: Textures/Icons/icon-40.png
///     - Name: pulse
///       Image: Textures/Icons/icon-20.png
///       Shader: Shaders/pulse.frag
/// </code>
/// </example>
public struct SpriteLayer
{
    /// <summary>Gets or sets the name of the layer, which is what a report about the layer says.</summary>
    public string? Name;

    /// <summary>Gets or sets the path of the image of the layer, relative to the game root.</summary>
    /// <remarks>
    /// The field a document writes, so a layer names its image the way a sprite names one. An image that is not there is drawn
    /// as the placeholder of the texture service and reported once, and <c>Age.Content.Lint</c> checks the path against the
    /// files of a build, so a path that is wrong fails a build rather than reaching the placeholder. A layer that names no
    /// image draws a solid quad, which is what a layer that only shades a rectangle of colour wants.
    /// </remarks>
    [ResourcePath]
    public string? Image;

    /// <summary>Gets or sets the path of the fragment stage that draws the layer, relative to the game root.</summary>
    /// <remarks>
    /// A stage is written under the header of the engine, which names the image of the layer as <c>TEXTURE</c> and the time of
    /// the frame as <c>TIME</c>, so a layer is what a sprite is made of when a picture of it is not the one the artist drew. A
    /// layer that names no shader is drawn with the program of the engine, and a shader that cannot be loaded is reported once
    /// and the layer is drawn without it, which is what an image that is not there does as well.
    /// </remarks>
    [ResourcePath]
    public string? Shader;
}
