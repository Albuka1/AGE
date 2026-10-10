using System.Text.Json;

namespace Age.Rendering;

/// <summary>
/// What draws a layer of a sprite: the stages of a shader and the values of the uniforms it reads.
/// </summary>
/// <remarks>
/// <para>
/// A material is the description of a program of the content of a game, so a sprite names a material rather than a document that
/// draws it: layers that are drawn by one program name that program, and the values that vary between them are data rather than
/// another file of GLSL.
/// </para>
/// <para>
/// A material is either read from the content or written where the layer is: <see cref="Id"/> names a prototype of the kind
/// <c>material</c> that a document declares, which is what keeps the path of a stage and the values of a uniform in one place, and
/// <see cref="Fragment"/> names the stage itself for a layer that has no document behind it. A material that names neither draws
/// the layer with the program of the engine, which is what an unshaded layer is.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// - type: Sprite
///   Layers:
///     - Name: base
///       Image: Textures/Tiles/tiles.bmp
///     - Name: glow
///       Image: Textures/Tiles/tiles.bmp
///       Material:
///         Id: pulse
/// </code>
/// </example>
public struct Material
{
    /// <summary>Gets or sets the identifier of the material of the content, or null when the layer names its stages itself.</summary>
    /// <remarks>
    /// The field a document writes, so a prototype of a material is the one place that holds the stages of a program and the
    /// values it starts with: a sprite that names the identifier is drawn with what the document declares, and a value of
    /// <see cref="Uniforms"/> overrides one of them for that layer alone. <c>Age.Content.Lint</c> checks the identifier against
    /// the material of a build, so a name that no document declares fails a build rather than a frame.
    /// </remarks>
    public string? Id;

    /// <summary>Gets or sets the path of the fragment stage of the layer, relative to the game root, or null to draw with the stage of the material or of the engine.</summary>
    /// <remarks>
    /// This is the frame of the material: the values of <see cref="Uniforms"/> are the ones a game varies while it runs, and the
    /// path of the stage is the one thing about a program that does not change. A layer that writes it draws with it rather than
    /// with the stage of the prototype it names, which is what a game that shades one sprite differently wants.
    /// </remarks>
    [Age.Core.ResourcePath]
    public string? Fragment;

    /// <summary>Gets or sets the path of the vertex stage of the layer, relative to the game root, or null to draw with the stage of the engine.</summary>
    /// <remarks>A material is usually a fragment stage alone, because the engine places the quad of a layer itself. A vertex stage of its own is what a layer that moves the quad of a sprite needs, which takes the placement over.</remarks>
    [Age.Core.ResourcePath]
    public string? Vertex;

    /// <summary>Gets or sets the values of the uniforms of the material, in the order they are sent, or null when the material reads none.</summary>
    /// <remarks>
    /// A value belongs to one layer rather than to the program, and a document writes it the way GLSL names its type, so a value
    /// that a stage reads as a float and one it reads as an int are told apart:
    /// <c>Uniforms: { Speed: { float: 4.0 }, Tint: { color: 255, 220, 120, 255 } }</c>. A material that names no value keeps the
    /// one the stage was written with, and a name that the stage does not declare is ignored by the device, which is what a
    /// uniform of one stage and not another looks like in practice.
    /// </remarks>
    public Dictionary<string, Dictionary<string, JsonElement>>? Uniforms;
}
