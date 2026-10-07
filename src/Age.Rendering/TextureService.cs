using Age.Assets;
using Age.Core;

namespace Age.Rendering;

/// <summary>
/// The default <see cref="ITextureService"/>. It decodes through <see cref="IImageLoader"/>, uploads through
/// <see cref="IRenderer"/>, and keeps the slot of every texture in a <see cref="ResourcePool{T}"/> keyed by path.
/// </summary>
/// <remarks>
/// A handle carries the slot it was issued from, so a handle from before an unload stops resolving instead of pointing
/// at the texture that replaced it. The service owns the device textures: disposing it releases them all.
/// </remarks>
public sealed class TextureService : ITextureService, IDisposable
{
    private readonly IImageLoader _images;
    private readonly IRenderer _renderer;
    private readonly ResourcePool<uint> _textures = new();

    /// <summary>Initializes the service with the decoder and the renderer it works through.</summary>
    /// <param name="images">The loader that decodes the image files.</param>
    /// <param name="renderer">The renderer that owns the device textures.</param>
    /// <exception cref="ArgumentNullException">The loader or the renderer is null.</exception>
    public TextureService(IImageLoader images, IRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(renderer);
        _images = images;
        _renderer = renderer;
    }

    /// <inheritdoc />
    public int Count => _textures.Count;

    /// <inheritdoc />
    public TextureHandle Load(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (_textures.TryGetHandle(relativePath, out ResourceHandle slot) && _textures.TryGet(slot, out uint cached))
        {
            return new TextureHandle(slot, (int)cached);
        }

        ImageData image = _images.Load(relativePath);
        TextureHandle uploaded = _renderer.CreateTexture(image.Pixels, image.Width, image.Height);
        slot = _textures.Add((uint)uploaded.Id, relativePath);
        return new TextureHandle(slot, uploaded.Id);
    }

    /// <inheritdoc />
    public bool IsAlive(TextureHandle texture) => texture.Resource.IsValid && _textures.TryGet(texture.Resource, out _);

    /// <inheritdoc />
    public bool Unload(TextureHandle texture)
    {
        if (!_textures.TryGet(texture.Resource, out uint id))
        {
            return false;
        }

        _renderer.ReleaseTexture(new TextureHandle((int)id));
        _textures.Release(texture.Resource);
        return true;
    }

    /// <inheritdoc />
    public void UnloadAll() => _textures.Clear(id => _renderer.ReleaseTexture(new TextureHandle((int)id)));

    /// <summary>Releases every texture. The service cannot be used afterwards.</summary>
    public void Dispose() => UnloadAll();
}
