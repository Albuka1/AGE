using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Draws every entity that has both a transform and a sprite, in ascending <see cref="SpriteComponent.ZOrder"/>, and
/// skips the sprites of a zero size. This type is not an <see cref="ISystem"/>; a <see cref="RenderPipeline"/> runs it
/// as the pass of the world.
/// </summary>
/// <remarks>
/// A camera that has a viewport size culls: a sprite whose box, rotated by the transform, does not overlap
/// <see cref="Camera2D.VisibleWorld"/> is not drawn at all. A camera that leaves the viewport size at zero culls nothing,
/// so the renderer keeps drawing everything for a game that never set it.
/// </remarks>
public sealed class RenderSystem : IRenderPass
{
    private readonly IRenderer _renderer;
    private readonly SpriteSorter _sorter;
    private readonly ITextureService? _textures;
    private readonly ISpriteSheetService? _sheets;
    private readonly List<Entity> _sprites = new();

    /// <summary>Initializes the system with a renderer and a sorter.</summary>
    /// <param name="renderer">The renderer that draws the sprites.</param>
    /// <param name="sorter">The sorter that puts them in the order of their <see cref="SpriteComponent.ZOrder"/>.</param>
    /// <param name="textures">The service that resolves the image a sprite names, or null to draw only the handles a game set.</param>
    /// <param name="sheets">The service that resolves the frame a sprite names, or null to draw only whole images.</param>
    /// <remarks>A container passes both services, which is what lets a sprite of a prototype or a scene name its image and the part of it to draw.</remarks>
    public RenderSystem(IRenderer renderer, SpriteSorter sorter, ITextureService? textures = null, ISpriteSheetService? sheets = null)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(sorter);
        _renderer = renderer;
        _sorter = sorter;
        _textures = textures;
        _sheets = sheets;
    }

    /// <inheritdoc />
    public void Render(World world, in Camera2D camera)
    {
        ArgumentNullException.ThrowIfNull(world);

        _renderer.SetCamera(camera);
        _renderer.BeginFrame(true);

        CollectSprites(world, camera);
        _sorter.Sort(_sprites, entity => world.Get<SpriteComponent>(entity).ZOrder);

        foreach (Entity entity in _sprites)
        {
            SpriteComponent sprite = world.Get<SpriteComponent>(entity);
            TransformComponent transform = world.Get<TransformComponent>(entity);
            SpriteRegion region = Region(world, entity, sprite);
            Vector2 size = (sprite.Size == Vector2.Zero ? region.Cell : sprite.Size) * transform.Scale;
            if (size == Vector2.Zero)
            {
                continue;
            }

            _renderer.DrawTextureRegion(region.Texture, region.Source, transform.Position, size, sprite.Color, transform.Rotation);
        }

        _renderer.EndFrame();
    }

    /// <summary>Returns the texture to draw for a sprite and the part of it that is drawn.</summary>
    /// <param name="world">The world that holds the sprite, which a resolved handle is written back into.</param>
    /// <param name="entity">The entity that carries the sprite.</param>
    /// <param name="sprite">The sprite that is being drawn.</param>
    /// <returns>The region, which is the whole image for a sprite that names no sheet.</returns>
    /// <remarks>
    /// A handle that was resolved is written back into the component, so an image is asked for once and every frame after
    /// the first draws what it resolved. A sprite without a path and without a handle is drawn as a solid color quad,
    /// which is what the renderer draws for a zero identifier, and one whose image or state is missing is drawn as the
    /// placeholder of the texture service.
    /// </remarks>
    private SpriteRegion Region(World world, Entity entity, in SpriteComponent sprite)
    {
        if (sprite.SheetPath is string sheetPath && _sheets is not null)
        {
            SpriteRegion region = _sheets.Resolve(sheetPath, sprite.State ?? string.Empty, sprite.Frame);
            world.GetRef<SpriteComponent>(entity).Texture = region.Texture;
            return region;
        }

        return SpriteRegion.Whole(Texture(world, entity, sprite), sprite.Size);
    }

    /// <summary>Returns the texture to draw for a sprite, resolving the image that content named.</summary>
    /// <param name="world">The world that holds the sprite, which the resolved handle is written back into.</param>
    /// <param name="entity">The entity that carries the sprite.</param>
    /// <param name="sprite">The sprite that is being drawn.</param>
    /// <returns>The handle of the image, or the placeholder when the image is not there.</returns>
    /// <remarks>
    /// The handle is written back into the component, so the image is asked for once and every frame after the first draws
    /// what it resolved. A sprite without a path and without a handle is drawn as a solid color quad, which is what the
    /// renderer draws for a zero identifier, and a sprite whose image is missing is drawn as the placeholder of the
    /// texture service.
    /// </remarks>
    private TextureHandle Texture(World world, Entity entity, in SpriteComponent sprite)
    {
        if (sprite.Texture.Id != 0 || sprite.TexturePath is not string path || _textures is null)
        {
            return sprite.Texture;
        }

        TextureHandle resolved = _textures.Resolve(path);
        world.GetRef<SpriteComponent>(entity).Texture = resolved;
        return resolved;
    }

    /// <summary>
    /// Collects the sprites of the world that the camera can see, in ascending entity order. A camera without a viewport
    /// size culls nothing, because the rectangle it covers is unknown.
    /// </summary>
    private void CollectSprites(World world, in Camera2D camera)
    {
        _sprites.Clear();

        Rect visible = camera.VisibleWorld;
        bool cull = visible.Width > 0f && visible.Height > 0f;
        Aabb view = Aabb.FromRect(visible);

        foreach (Entity entity in world.Enumerate<SpriteComponent>())
        {
            if (!world.Has<TransformComponent>(entity))
            {
                continue;
            }

            TransformComponent transform = world.Get<TransformComponent>(entity);
            Vector2 size = world.Get<SpriteComponent>(entity).Size * transform.Scale;

            if (cull && !SpriteQuad.Bounds(transform.Position, size, transform.Rotation).Intersects(view))
            {
                continue;
            }

            _sprites.Add(entity);
        }
    }
}
