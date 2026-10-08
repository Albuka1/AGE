using System.Runtime.ExceptionServices;
using Age.Assets;
using Age.Core;

namespace Age.Rendering;

/// <summary>
/// The default <see cref="IFontService"/>. It reads the font file through <see cref="IAssetLoader"/>, bakes it with
/// <see cref="TrueTypeFontBake"/>, uploads the atlas through <see cref="IRenderer"/> and draws the glyphs with
/// <see cref="IRenderer.DrawTextureRegion"/>.
/// </summary>
/// <remarks>
/// The slot of every baked font lives in a <see cref="ResourcePool{TKey, T}"/>, keyed by the pair of the path and the height, so a handle
/// from before an unload stops resolving instead of pointing at the atlas that replaced it. The service owns the device
/// textures of the atlases: disposing it releases them all, and a disposed service refuses to load another font while
/// <see cref="UnloadAll"/> stays available for what a renderer refused to release. It is not thread-safe, so call it
/// from the thread that owns the renderer's context. An atlas whose release the renderer refused stays loaded, so a
/// later call can retry it.
/// </remarks>
public sealed class FontService : IFontService, IDisposable
{
    private readonly IAssetLoader _assets;
    private readonly IRenderer _renderer;
    private readonly ResourcePool<(string Path, float Height), FontData> _fonts = new();
    private bool _disposed;

    /// <summary>Initializes the service with the loader of the font files and the renderer it draws through.</summary>
    /// <param name="assets">The loader that reads the bytes of the font files.</param>
    /// <param name="renderer">The renderer that owns the atlas textures and draws the glyphs.</param>
    /// <exception cref="ArgumentNullException">The loader or the renderer is null.</exception>
    public FontService(IAssetLoader assets, IRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(renderer);
        _assets = assets;
        _renderer = renderer;
    }

    /// <inheritdoc />
    public int Count => _fonts.Count;

    /// <inheritdoc />
    public FontHandle Load(string relativePath, float pixelHeight)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelHeight);

        if (!float.IsFinite(pixelHeight))
        {
            throw new ArgumentOutOfRangeException(nameof(pixelHeight), pixelHeight, "The height of a line has to be a finite number of pixels.");
        }

        // The key of a baked font is the pair of what it was baked from: the path of the file and the height of a line.
        // A string that joins them would have to be unambiguous, which is a property a tuple has and a string does not.
        var key = (Path: relativePath, Height: pixelHeight);

        if (_fonts.TryGetHandle(key, out ResourceHandle slot) && _fonts.TryGet(slot, out FontData? cached) && cached is not null)
        {
            return new FontHandle(slot, cached.Texture.Id);
        }

        FontAtlas atlas = TrueTypeFontBake.Bake(_assets.Load<byte[]>(relativePath), pixelHeight, TrueTypeFontBake.AsciiCharacters);
        TextureHandle texture = _renderer.CreateTexture(atlas.Pixels, atlas.Width, atlas.Height);

        try
        {
            slot = _fonts.Add(new FontData(atlas, texture), key);
        }
        catch (Exception)
        {
            // The atlas is registered nowhere, so delete it here instead of leaking the device texture.
            _renderer.ReleaseTexture(texture);
            throw;
        }

        return new FontHandle(slot, texture.Id);
    }

    /// <inheritdoc />
    public bool IsAlive(FontHandle font) => font.Resource.IsValid && _fonts.TryGet(font.Resource, out _);

    /// <inheritdoc />
    public bool Unload(FontHandle font)
    {
        if (!_fonts.TryGet(font.Resource, out FontData? data) || data is null)
        {
            return false;
        }

        _renderer.ReleaseTexture(data.Texture);
        _fonts.Release(font.Resource);
        return true;
    }

    /// <inheritdoc />
    public void UnloadAll()
    {
        ExceptionDispatchInfo? failure = null;

        // Release one atlas at a time and forget a slot only once its release succeeded, so a renderer that refuses one
        // atlas leaves it loaded for a later attempt and every other atlas still unloads in this call.
        foreach (ResourceHandle slot in _fonts.GetHandles())
        {
            if (!_fonts.TryGet(slot, out FontData? data) || data is null)
            {
                continue;
            }

            try
            {
                _renderer.ReleaseTexture(data.Texture);
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
                continue;
            }

            _fonts.Release(slot);
        }

        failure?.Throw();
    }

    /// <inheritdoc />
    public Vector2 Measure(FontHandle font, ReadOnlySpan<char> text)
    {
        FontData data = Require(font);
        float width = 0f;

        foreach (char character in text)
        {
            width += data.Atlas.Glyph(character).Advance;
        }

        return new Vector2(width, data.Atlas.LineHeight);
    }

    /// <inheritdoc />
    public void Draw(FontHandle font, ReadOnlySpan<char> text, Vector2 position, Color color)
    {
        FontData data = Require(font);
        float pen = position.X;
        float baseline = position.Y + data.Atlas.Ascent;

        foreach (char character in text)
        {
            FontGlyph glyph = data.Atlas.Glyph(character);

            if (glyph.Size.X > 0f && glyph.Size.Y > 0f)
            {
                _renderer.DrawTextureRegion(
                    data.Texture,
                    glyph.Source,
                    new Vector2(pen + glyph.Bearing.X, baseline + glyph.Bearing.Y),
                    glyph.Size,
                    color);
            }

            pen += glyph.Advance;
        }
    }

    /// <summary>Releases every atlas and marks the service as disposed, so it cannot load another font.</summary>
    /// <remarks>
    /// The call is idempotent, and <see cref="UnloadAll"/> stays available afterwards, which is what retries an atlas
    /// that the renderer refused to release.
    /// </remarks>
    public void Dispose()
    {
        _disposed = true;
        UnloadAll();
    }

    /// <summary>Returns the font behind the handle, or throws when the handle is stale.</summary>
    private FontData Require(FontHandle font)
    {
        if (_fonts.TryGet(font.Resource, out FontData? data) && data is not null)
        {
            return data;
        }

        throw new InvalidOperationException("The handle is not a live font of this service. Load the font first, and do not use a handle after its font was unloaded.");
    }

    /// <summary>The atlas of a baked font and the device texture that it was uploaded to.</summary>
    private sealed class FontData
    {
        public FontData(FontAtlas atlas, TextureHandle texture)
        {
            Atlas = atlas;
            Texture = texture;
        }

        public FontAtlas Atlas { get; }

        public TextureHandle Texture { get; }
    }
}
