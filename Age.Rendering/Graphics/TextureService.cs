using System.Runtime.ExceptionServices;
using Age.Assets;
using Age.Core;
using Microsoft.Extensions.Logging;

namespace Age.Rendering;

/// <summary>
/// The default <see cref="ITextureService"/>. It decodes through <see cref="IImageLoader"/>, uploads through
/// <see cref="IRenderer"/>, and keeps the slot of every texture in a <see cref="ResourcePool{TKey, T}"/> keyed by path.
/// </summary>
/// <remarks>
/// A handle carries the slot it was issued from, so a handle from before an unload stops resolving instead of pointing
/// at the texture that replaced it. The service owns the device textures: disposing it releases them all. It is not
/// thread-safe, so call it from the thread that owns the renderer's context. A texture whose release the renderer
/// refused stays loaded, so a later call can retry it.
/// </remarks>
public sealed class TextureService : ITextureService, IDisposable
{
    /// <summary>The size of the placeholder, in pixels, which is the side of its square.</summary>
    private const int PlaceholderSide = 64;

    /// <summary>The size of the placeholder, which is what a sprite that drew it is drawn at.</summary>
    private static readonly Vector2 PlaceholderSize = new(PlaceholderSide, PlaceholderSide);

    private readonly IImageLoader _images;
    private readonly IRenderer _renderer;
    private readonly ILogger<TextureService>? _logger;
    private readonly ResourcePool<string, uint> _textures = new();
    private readonly List<string> _missing = [];
    private readonly HashSet<string> _broken = new(StringComparer.Ordinal);
    private readonly Dictionary<int, Vector2> _sizes = [];
    private readonly List<string> _order = [];
    private TextureHandle? _error;

    /// <summary>Initializes the service with the decoder and the renderer it works through.</summary>
    /// <param name="images">The loader that decodes the image files.</param>
    /// <param name="renderer">The renderer that owns the device textures.</param>
    /// <param name="logger">The logger that reports an image that is not there, or null to report nothing.</param>
    /// <exception cref="ArgumentNullException">The loader or the renderer is null.</exception>
    public TextureService(IImageLoader images, IRenderer renderer, ILogger<TextureService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(renderer);
        _images = images;
        _renderer = renderer;
        _logger = logger;
    }

    /// <inheritdoc />
    public int Count => _textures.Count;

    /// <inheritdoc />
    public TextureHandle Error => _error ??= CreateErrorTexture();

    /// <inheritdoc />
    public int MissingCount => _missing.Count;

    /// <inheritdoc />
    public IEnumerable<string> Missing => _missing;

    /// <inheritdoc />
    public TextureHandle Resolve(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (_textures.TryGetHandle(relativePath, out ResourceHandle slot) && _textures.TryGet(slot, out uint cached))
        {
            return new TextureHandle(slot, (int)cached);
        }

        if (_broken.Contains(relativePath))
        {
            return Error;
        }

        try
        {
            return Load(relativePath);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            // An image that content names and a build does not ship is the mistake a person looks for in the log, so it is
            // reported once per path rather than on every frame, and the sprite is drawn as the placeholder from here on.
            _broken.Add(relativePath);
            _missing.Add(relativePath);
            _logger?.LogError("The image '{Path}' is not there, so it is drawn as the placeholder: {Reason}", relativePath, exception.Message);
            return Error;
        }
    }

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

        // The size of the image is remembered beside its texture, so a sprite that names no size is drawn at the size of the file: the
        // device is not asked how large a texture it uploaded is, and the pixels are the one place the number is known.
        _sizes[uploaded.Id] = new Vector2(image.Width, image.Height);

        if (!_order.Contains(relativePath))
        {
            _order.Add(relativePath);
        }

        return new TextureHandle(slot, uploaded.Id);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A handle the renderer made itself is not one this service decoded, so its size is unknown and answers zero. The placeholder of
    /// this service is the one exception: its size is known without a lookup, because it is built here, so a game that resolves a path
    /// whose image is not there reads the size of the sprite it draws rather than a zero.
    /// </remarks>
    public Vector2 Size(TextureHandle texture)
    {
        if (texture.Id == _error?.Id)
        {
            return PlaceholderSize;
        }

        return _sizes.TryGetValue(texture.Id, out Vector2 size) ? size : Vector2.Zero;
    }

    /// <inheritdoc />
    public IEnumerable<(string Path, TextureHandle Texture, Vector2 Size)> Textures
    {
        get
        {
            foreach (string path in _order)
            {
                if (_textures.TryGetHandle(path, out ResourceHandle slot) && _textures.TryGet(slot, out uint id))
                {
                    yield return (path, new TextureHandle(slot, (int)id), _sizes.TryGetValue((int)id, out Vector2 size) ? size : Vector2.Zero);
                }
            }
        }
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
        _sizes.Remove((int)id);
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

        // The placeholder belongs to the renderer, which deletes it with its own objects: the service lets go of the
        // handle, so the next call asks for a new one rather than handing out a texture that is gone.
        _error = null;
        _sizes.Clear();
        _order.Clear();

        failure?.Throw();
    }

    /// <summary>Builds the texture that stands in for an image that is not there: a checkerboard that says what it is.</summary>
    /// <remarks>The texture needs no file, so a game that ships a broken path still shows something a person can see.</remarks>
    private TextureHandle CreateErrorTexture()
    {
        const int Size = PlaceholderSide;
        const int Cell = 16;

        byte[] pixels = new byte[Size * Size * 4];

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                bool light = ((x / Cell) + (y / Cell)) % 2 == 0;
                int index = ((y * Size) + x) * 4;

                // Magenta and near black, which no content draws on purpose: a sprite that cannot be missed.
                pixels[index] = light ? (byte)255 : (byte)32;
                pixels[index + 1] = 0;
                pixels[index + 2] = light ? (byte)255 : (byte)32;
                pixels[index + 3] = 255;
            }
        }

        const string Word = "ERROR";
        Write(pixels, Size, Word, (Size - (Word.Length * BitmapFontMetrics.GlyphWidth)) / 2, (Size - BitmapFontMetrics.GlyphHeight) / 2);

        TextureHandle handle = _renderer.CreateTexture(pixels, Size, Size);
        _sizes[handle.Id] = PlaceholderSize;
        return handle;
    }

    /// <summary>Writes a word of the built-in font into a buffer of pixels, which is how the placeholder says what it is.</summary>
    private static void Write(byte[] pixels, int size, ReadOnlySpan<char> word, int left, int top)
    {
        ReadOnlySpan<byte> glyphs = BuiltInFont.Data;

        foreach (char character in word)
        {
            int glyph = character < BitmapFontMetrics.GlyphCount ? character : 0;

            for (int row = 0; row < BitmapFontMetrics.GlyphHeight; row++)
            {
                byte bits = glyphs[(glyph * BitmapFontMetrics.GlyphHeight) + row];

                for (int column = 0; column < BitmapFontMetrics.GlyphWidth; column++)
                {
                    int x = left + column;

                    if ((bits & (1 << column)) == 0 || x < 0 || x >= size || top + row >= size)
                    {
                        continue;
                    }

                    int index = ((((top + row) * size) + x) * 4);
                    pixels[index] = 255;
                    pixels[index + 1] = 255;
                    pixels[index + 2] = 255;
                    pixels[index + 3] = 255;
                }
            }

            left += BitmapFontMetrics.GlyphWidth;
        }
    }

    /// <summary>Releases every texture. The service cannot be used afterwards.</summary>
    public void Dispose() => UnloadAll();
}
