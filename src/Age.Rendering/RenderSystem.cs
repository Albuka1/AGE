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
    private readonly List<Entity> _sprites = new();

    /// <summary>Initializes the system with a renderer and a sorter.</summary>
    public RenderSystem(IRenderer renderer, SpriteSorter sorter)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(sorter);
        _renderer = renderer;
        _sorter = sorter;
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
            Vector2 size = sprite.Size * transform.Scale;
            if (size == Vector2.Zero)
            {
                continue;
            }

            _renderer.DrawSprite(sprite.Texture, transform.Position, size, sprite.Color, transform.Rotation);
        }

        _renderer.EndFrame();
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
