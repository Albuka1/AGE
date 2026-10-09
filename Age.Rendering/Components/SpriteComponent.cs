using System.Text.Json.Serialization;
using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Draws the image that <see cref="TexturePath"/> names, the frame of the sheet that <see cref="SheetPath"/> and
/// <see cref="State"/> name, a handle that a game set, or a solid color quad when none of them is set.
/// </summary>
/// <remarks>
/// <para>
/// A sprite that content describes names what it draws by path, which is the one thing about a sprite that survives a save:
/// the handle belongs to the graphics device, and a device is not saved. A path is resolved the first time the sprite is
/// drawn, and an image that is not there becomes the placeholder of the texture service rather than a failed frame.
/// </para>
/// <para>
/// A sheet is the same idea for a character: the document next to the image says how the frames of a state lie on it, and
/// <see cref="State"/> and <see cref="Frame"/> pick one, so a game never writes a normalized coordinate. A sprite that
/// names a sheet and leaves <see cref="Size"/> at zero takes the size of one cell of it.
/// </para>
/// <para>
/// A layer of a character is an entity of its own: the engine draws in <see cref="ZOrder"/>, so a body, its clothes and the
/// effect over them are three entities at the same position with three orders, and each of them carries its own sheet and
/// its own animation.
/// </para>
/// </remarks>
[Component("Sprite")]
public struct SpriteComponent : IComponent
{
    /// <summary>Gets or sets the texture. A zero identifier renders a solid color quad.</summary>
    /// <remarks>
    /// This is the handle of a texture that a game loaded itself, and it is state of a run rather than data of content, so
    /// it is not part of what a scene or a prototype carries: a document that writes this field is refused rather than read
    /// without it, and a sprite that content describes names its image with <see cref="TexturePath"/>. A handle that the texture service no
    /// longer holds — one from before everything was unloaded, or one a renderer made itself — gives way to the path, which
    /// the renderer resolves again; a game that wants its own handle to win leaves the path unset.
    /// </remarks>
    [JsonIgnore]
    public TextureHandle Texture;

    /// <summary>Gets or sets the path of the image, relative to the game root.</summary>
    /// <remarks>
    /// The field a document writes, so a prototype or a scene names a sprite the way a game loads an asset. An image that
    /// is not there is drawn as the placeholder of the texture service and reported once, which makes a mistake in the
    /// content of a game visible rather than fatal. <c>Age.Content.Lint</c> checks it against the files of a build, so a
    /// path that is wrong fails a build rather than reaching the placeholder at all.
    /// </remarks>
    [ResourcePath]
    public string? TexturePath;

    /// <summary>Gets or sets the path of the document of a sprite sheet, relative to the game root.</summary>
    /// <remarks>
    /// A sheet wins over <see cref="TexturePath"/>, because the frames of a state are what a character draws. The document
    /// is the one next to the image, in the catalogue of the repository, and it says the grid, the states and how fast
    /// they play: see <c>Age.Content.Sheets.SpriteSheetReader</c>.
    /// </remarks>
    [ResourcePath]
    public string? SheetPath;

    /// <summary>Gets or sets the name of the state of the sheet that is drawn.</summary>
    /// <remarks>A state that the sheet does not declare is drawn as the placeholder and reported once.</remarks>
    public string? State;

    /// <summary>Gets or sets the frame of the state that is drawn, counting from zero.</summary>
    /// <remarks>A <see cref="SpriteAnimationSystem"/> writes this field, and a game may set it for a state that stands still.</remarks>
    public int Frame;

    /// <summary>Gets or sets the base size, in pixels, before the transform scale is applied.</summary>
    public Vector2 Size;

    /// <summary>Gets or sets the tint color.</summary>
    public Color Color;

    /// <summary>Gets or sets the draw order. Larger values draw on top.</summary>
    public int ZOrder;
}
