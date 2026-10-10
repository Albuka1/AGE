using Age.Core;

namespace Age.Rendering;

/// <summary>
/// One layer of a <see cref="SpriteComponent"/>: the image it draws and the material that draws it.
/// </summary>
/// <remarks>
/// <para>
/// A layer is what a sprite is made of when one image is not enough: the body of a character, the clothes over it and the glow
/// that breathes are the layers of one entity, each with an image and a material of its own, rather than three entities that
/// follow one another. The layers of a sprite are drawn in the order the document writes them, so the first is the one at the
/// bottom, and every layer is drawn at the position, the size and the colour of the sprite.
/// </para>
/// <para>
/// A layer that names no material is drawn with the program of the engine, which is what an unshaded layer is: the tint of the
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
///       Material:
///         Id: Pulse
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

    /// <summary>Gets or sets what draws the layer: the stages of a shader and the values of the uniforms it reads.</summary>
    /// <remarks>
    /// A layer is <see cref="Material"/> in the shape a document writes it, which is the reason a path of a stage is written
    /// once for every layer that shares it rather than once per layer: name the material of the content, and the values that
    /// differ between two layers are data of them. A layer that names no material is drawn with the program of the engine, and a
    /// shader that cannot be loaded is reported once and the layer is drawn without it, which is what an image that is not there
    /// does as well.
    /// </remarks>
    public Material Material;
}
