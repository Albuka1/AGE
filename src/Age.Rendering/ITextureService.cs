namespace Age.Rendering;

/// <summary>
/// Loads images into device textures and owns their lifetime. Textures are cached by path, so loading the same image
/// twice returns the same texture instead of uploading it again.
/// </summary>
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

    /// <summary>Returns the texture of the image at the given path, decoding and uploading it on the first call.</summary>
    /// <param name="relativePath">The path of the image, relative to the game root.</param>
    /// <returns>The texture of the image.</returns>
    /// <exception cref="ArgumentException">The path is null, empty or whitespace.</exception>
    /// <exception cref="InvalidDataException">The file is not an image in a supported format.</exception>
    /// <exception cref="FileNotFoundException">No file exists at that path.</exception>
    /// <exception cref="InvalidOperationException">The renderer has not been attached to a window.</exception>
    TextureHandle Load(string relativePath);

    /// <summary>Determines whether the handle still refers to a texture that this service loaded.</summary>
    /// <param name="texture">The handle to check.</param>
    /// <returns><see langword="true"/> while the texture is loaded. A handle the renderer made itself, and a handle from before an unload, both return <see langword="false"/>.</returns>
    bool IsAlive(TextureHandle texture);

    /// <summary>Deletes the texture behind the handle and forgets its path, so loading the path again decodes it anew.</summary>
    /// <param name="texture">The handle of the texture to delete.</param>
    /// <returns><see langword="true"/> when a loaded texture was deleted, <see langword="false"/> when the handle was stale or not owned by this service.</returns>
    bool Unload(TextureHandle texture);

    /// <summary>Deletes every texture that this service loaded.</summary>
    void UnloadAll();
}
