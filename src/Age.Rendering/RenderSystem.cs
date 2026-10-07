using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Draws every entity that has both a transform and a sprite, in ascending <see cref="SpriteComponent.ZOrder"/>, and
/// skips the sprites of a zero size. This type is not an <see cref="ISystem"/>; a <see cref="RenderPipeline"/> runs it
/// as the pass of the world.
/// </summary>
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

        CollectSprites(world);
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

            _renderer.DrawSprite(sprite.Texture, transform.Position, size, sprite.Color);
        }

        _renderer.EndFrame();
    }

    private void CollectSprites(World world)
    {
        _sprites.Clear();

        foreach (Entity entity in world.Enumerate<SpriteComponent>())
        {
            if (world.Has<TransformComponent>(entity))
            {
                _sprites.Add(entity);
            }
        }
    }
}
