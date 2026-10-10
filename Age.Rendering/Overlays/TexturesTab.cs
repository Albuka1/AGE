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
/// sprites that name a path is what says which image is worth looking at.
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
    }

    /// <inheritdoc />
    public void Render(IRenderer renderer, Rect body)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        List<(string Path, Vector2 Size)> rows = _textures.Textures.Select(entry => (entry.Path, entry.Size)).ToList();
        World world = _world();

        // The header stands above the images and the hint below them, so the room an image row may take is what is left of the body
        // once those two are accounted for: a page that counted only the image rows would draw its last ones past the body and have
        // them cut off by the clip of the window.
        int height = Math.Max(1, (int)(body.Height / _line));
        int capacity = Math.Max(0, height - 2);
        int shown = Math.Min(rows.Count, capacity);
        float y = body.Y;

        renderer.DrawText($"{rows.Count} images, {_textures.MissingCount} that did not resolve", new Vector2(body.X, y), HintColour);
        y += _line;

        for (var index = 0; index < shown; index++)
        {
            (string path, Vector2 size) = rows[index];
            int used = SpritesUsing(world, path);
            string line = $"{path} {size.X:0}x{size.Y:0} x{used}";

            renderer.DrawText(line, new Vector2(body.X, y), used > 0 ? TextColour : CountColour);
            y += _line;
        }

        if (rows.Count == 0)
        {
            renderer.DrawText("(no image was resolved yet)", new Vector2(body.X, y), HintColour);
        }
        else if (shown < rows.Count)
        {
            renderer.DrawText($"... {rows.Count - shown} more", new Vector2(body.X, y), HintColour);
        }
    }

    /// <summary>Returns how many sprites of the world draw the image of a path, which is what says which images matter.</summary>
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
