using Age.Core;

namespace Age.Rendering;

/// <summary>
/// The page of a developer window that gathers the images a run resolved: each path, the size it was decoded at, and how many sprites
/// of the world name it.
/// </summary>
/// <remarks>
/// <para>
/// A person looking for the placeholder that appeared on screen finds it here: the path of every image the run asked for is listed
/// with its size, so an image that is not there, and the sprite that drew it as the checkerboard, are both named rather than hunted for
/// in the content.
/// </para>
/// <para>
/// The world is read through a delegate rather than held, because a game replaces its world when it loads a scene, and the count of
/// sprites that name a path is what says which image is worth looking at. The list is longer than the body of the page in a run of any
/// length, so a wheel over the body scrolls it and a bar at the right edge says where a person stands in it.
/// </para>
/// </remarks>
public sealed class TexturesTab : IDevWindowTab
{
    private static readonly Color TextColour = new(225, 225, 225);
    private static readonly Color HintColour = new(150, 150, 150);
    private static readonly Color CountColour = new(180, 200, 230);

    private readonly ITextureService _textures;
    private readonly Func<World> _world;
    private readonly float _line = BitmapFontMetrics.GlyphHeight + 4f;
    private readonly List<(string Path, Vector2 Size)> _rows = [];

    private ScrollState _scroll;

    /// <summary>Initializes the page with the textures it reports and the world whose sprites name them.</summary>
    /// <param name="textures">The service that answers the images the run resolved.</param>
    /// <param name="world">The world to read, which is asked for on every frame so a scene that replaced it is reported.</param>
    /// <exception cref="ArgumentNullException">The service or the world is null.</exception>
    public TexturesTab(ITextureService textures, Func<World> world)
    {
        ArgumentNullException.ThrowIfNull(textures);
        ArgumentNullException.ThrowIfNull(world);

        _textures = textures;
        _world = world;
    }

    /// <inheritdoc />
    public string Title => "textures";

    /// <inheritdoc />
    public bool Closable => true;

    /// <inheritdoc />
    public void Update(in GameTime frame, Rect body, in WindowPointer pointer)
    {
        _rows.Clear();

        foreach ((string path, TextureHandle _, Vector2 size) in _textures.Textures)
        {
            _rows.Add((path, size));
        }

        // The header of the page takes a row of its own, so the scroll is measured over the rows of images plus that one:
        // a page that counted only the images would draw its last ones past the body and have them cut off by the clip of the
        // window, which is what a scroll that is one row short looks like.
        _scroll.Update(pointer, body, _rows.Count + 1, _line);
    }

    /// <inheritdoc />
    public void Render(IRenderer renderer, Rect body)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        int visible = ScrollState.Visible(body, _line);
        int total = _rows.Count + 1;
        int count = Math.Min(total, visible);
        int start = Math.Clamp(_scroll.First, 0, Math.Max(0, total - count));

        World world = _world();

        renderer.PushClip(body);

        try
        {
            float y = body.Y;

            for (var index = 0; index < count; index++)
            {
                int row = start + index;

                if (row == 0)
                {
                    renderer.DrawText($"{_rows.Count} images, {_textures.MissingCount} that did not resolve", new Vector2(body.X, y), HintColour);
                    y += _line;
                    continue;
                }

                (string path, Vector2 size) = _rows[row - 1];
                int used = SpritesUsing(world, path);
                string line = $"{path} {size.X:0}x{size.Y:0} x{used}";

                renderer.DrawText(line, new Vector2(body.X, y), used > 0 ? TextColour : CountColour);
                y += _line;
            }

            if (_rows.Count == 0)
            {
                renderer.DrawText("(no image was resolved yet)", new Vector2(body.X, y), HintColour);
            }
        }
        finally
        {
            renderer.PopClip();
        }

        _scroll.DrawBar(renderer, body, total, _line);
    }

    private static int SpritesUsing(World world, string path)
    {
        var count = 0;

        foreach (Entity entity in world.Enumerate<SpriteComponent>())
        {
            SpriteComponent sprite = world.Get<SpriteComponent>(entity);

            if (string.Equals(sprite.TexturePath, path, StringComparison.Ordinal) || string.Equals(sprite.SheetPath, path, StringComparison.Ordinal))
            {
                count++;
                continue;
            }

            if (sprite.Layers is { Length: > 0 } layers && layers.Any(layer => string.Equals(layer.Image, path, StringComparison.Ordinal)))
            {
                count++;
            }
        }

        return count;
    }
}
