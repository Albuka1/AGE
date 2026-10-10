using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Loads images into device textures and owns their lifetime. Textures are cached by path, so loading the same image
/// twice returns the same texture instead of uploading it again.
/// </summary>
/// <remarks>
/// The service owns every texture it created, and it is not thread-safe: call it from the thread that owns the
/// renderer's context, which is the thread that runs the game loop.
/// </remarks>
/// <example>
/// <code>
/// TextureHandle player = textures.Load("art/player.png");
/// world.Set(entity, new SpriteComponent { Texture = player, Size = new Vector2(64f, 64f), Color = Color.White });
///
/// textures.Unload(player);
/// </code>
/// </example>
public interface ITextureService
{
    /// <summary>Gets the number of textures that are currently loaded.</summary>
    int Count { get; }

    /// <summary>Gets the texture that stands in for an image that is not there.</summary>
    /// <value>A built-in checkerboard that needs no file and has the word ERROR written on it.</value>
    /// <remarks>A game does not have to draw it: <see cref="Resolve"/> answers with it for a path that cannot be loaded.</remarks>
    TextureHandle Error { get; }

    /// <summary>Gets the number of paths that could not be resolved.</summary>
    /// <remarks>This is the number a developer looks at when something in the game is drawn as the placeholder.</remarks>
    int MissingCount { get; }

    /// <summary>Gets the paths that could not be resolved, in the order they were first asked about.</summary>
    IEnumerable<string> Missing { get; }

    /// <summary>Returns the texture of the image at the given path, or the placeholder when the image is not there.</summary>
    /// <param name="relativePath">The path of the image, relative to the game root.</param>
    /// <returns>The texture of the image, or <see cref="Error"/> when the file is missing or is not an image.</returns>
    /// <exception cref="ArgumentException">The path is null, empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The renderer has not been attached to a window, or the path escapes the game root.</exception>
    /// <remarks>
    /// This is how content reaches a sprite: a prototype or a scene names an image by path, and the image is decoded and
    /// uploaded the first time something asks for it. A path that cannot be loaded is reported once rather than on every
    /// frame, and the sprite is drawn as <see cref="Error"/>, because an image that is not there is a mistake in the
    /// content of a game rather than a reason for a frame to fail. A path that escapes the game root is refused by the
    /// loader rather than drawn as the placeholder, because that is a mistake in a game rather than in its content.
    /// </remarks>
    TextureHandle Resolve(string relativePath);

    /// <summary>Returns the texture of the image at the given path, decoding and uploading it on the first call.</summary>
    /// <param name="relativePath">The path of the image, relative to the game root.</param>
    /// <returns>The texture of the image.</returns>
    /// <remarks>
    /// An upload that cannot be registered, because another caller registered the same path while the image was decoded,
    /// is deleted again instead of leaking the device texture.
    /// </remarks>
    /// <exception cref="ArgumentException">The path is null, empty or whitespace.</exception>
    /// <exception cref="InvalidDataException">The file is not an image in a supported format.</exception>
    /// <exception cref="FileNotFoundException">No file exists at that path.</exception>
    /// <exception cref="InvalidOperationException">The renderer has not been attached to a window.</exception>
    TextureHandle Load(string relativePath);

    /// <summary>Determines whether the handle still refers to a texture that this service loaded.</summary>
    /// <param name="texture">The handle to check.</param>
    /// <returns><see langword="true"/> while the texture is loaded. A handle the renderer made itself, and a handle from before an unload, both return <see langword="false"/>.</returns>
    bool IsAlive(TextureHandle texture);

    /// <summary>Returns the size of a loaded texture in pixels, which is what a sprite that names no size is drawn at.</summary>
    /// <param name="texture">The handle of the texture.</param>
    /// <returns>The width and the height of the image, or zero for a handle this service did not load and does not know the size of.</returns>
    /// <remarks>
    /// The size is remembered when the image is decoded, because the device is not asked how large a texture it uploaded is: a sprite
    /// that leaves <c>Size</c> at zero draws at the size of the image it names, and content says nothing about a number the file
    /// already holds.
    /// </remarks>
    Vector2 Size(TextureHandle texture);

    /// <summary>Returns every image this service loaded, with the path that named it and the size it was decoded at.</summary>
    /// <returns>The textures in the order they were first asked about, which is what a developer page reports.</returns>
    /// <remarks>A page that gathers the textures of a run reads this rather than walking the pool the service keeps to itself.</remarks>
    IEnumerable<(string Path, TextureHandle Texture, Vector2 Size)> Textures { get; }

    /// <summary>Deletes the texture behind the handle and forgets its path, so loading the path again decodes it anew.</summary>
    /// <param name="texture">The handle of the texture to delete.</param>
    /// <returns><see langword="true"/> when a loaded texture was deleted, <see langword="false"/> when the handle was stale or not owned by this service.</returns>
    bool Unload(TextureHandle texture);

    /// <summary>Deletes every texture that this service loaded.</summary>
    /// <remarks>
    /// A texture whose release the renderer refused stays loaded, so another call retries it, and every other texture is
    /// still released. The first refusal is reported, after the rest were unloaded.
    /// </remarks>
    void UnloadAll();
}
