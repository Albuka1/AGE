using System.Runtime.ExceptionServices;
using Age.Assets;
using Age.Core;

namespace Age.Rendering;

/// <summary>
/// The default <see cref="ITextureService"/>. It decodes through <see cref="IImageLoader"/>, uploads through
/// <see cref="IRenderer"/>, and keeps the slot of every texture in a <see cref="ResourcePool{T}"/> keyed by path.
/// </summary>
/// <remarks>
/// A handle carries the slot it was issued from, so a handle from before an unload stops resolving instead of pointing
/// at the texture that replaced it. The service owns the device textures: disposing it releases them all. It is not
/// thread-safe, so call it from the thread that owns the renderer's context. A texture whose release the renderer
/// refused stays loaded, so a later call can retry it.
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

        try
        {
            slot = _textures.Add((uint)uploaded.Id, relativePath);
        }
        catch (Exception)
        {
            // The upload is registered nowhere, so delete it here instead of leaking the device texture.
            _renderer.ReleaseTexture(uploaded);
            throw;
        }

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
    public void UnloadAll()
    {
        ExceptionDispatchInfo? failure = null;

        // Release one texture at a time and forget a slot only once its release succeeded, so a renderer that refuses
        // one texture leaves it loaded for a later attempt and every other texture still unloads in this call.
        foreach (ResourceHandle slot in _textures.GetHandles())
        {
            if (!_textures.TryGet(slot, out uint id))
            {
                continue;
            }

            try
            {
                _renderer.ReleaseTexture(new TextureHandle((int)id));
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
                continue;
            }

            _textures.Release(slot);
        }

        failure?.Throw();
    }

    /// <summary>Releases every texture. The service cannot be used afterwards.</summary>
    public void Dispose() => UnloadAll();
}
