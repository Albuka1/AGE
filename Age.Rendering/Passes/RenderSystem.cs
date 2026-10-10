using Age.Core;
using Microsoft.Extensions.Logging;

namespace Age.Rendering;

/// <summary>
/// Draws every entity that has both a transform and a sprite, in ascending <see cref="SpriteComponent.ZOrder"/>, and
/// skips the sprites of a zero size. This type is not an <see cref="ISystem"/>; a <see cref="RenderPipeline"/> runs it
/// as the pass of the world.
/// </summary>
/// <remarks>
/// A camera that has a viewport size culls: a sprite whose box, rotated by the transform, does not overlap
/// <see cref="Camera2D.VisibleWorld"/> is not drawn at all. A camera that leaves the viewport size at zero culls nothing,
/// so the renderer keeps drawing everything for a game that never set it. A sprite of <see cref="SpriteComponent.Layers"/> is
/// drawn a quad per layer, each of them with the shader the layer names, which is the one thing of this pass that draws with a
/// program of a game rather than with the program of the engine.
/// </remarks>
public sealed class RenderSystem : IRenderPass
{
    private readonly IRenderer _renderer;
    private readonly SpriteSorter _sorter;
    private readonly ITextureService? _textures;
    private readonly ISpriteSheetService? _sheets;
    private readonly IMaterialService? _materials;
    private readonly ILogger<RenderSystem>? _logger;
    private readonly List<Entity> _sprites = new();
    private ShaderHandle _currentShader;
    private uint _generation;

    /// <summary>Initializes the system with a renderer and a sorter.</summary>
    /// <param name="renderer">The renderer that draws the sprites.</param>
    /// <param name="sorter">The sorter that puts them in the order of their <see cref="SpriteComponent.ZOrder"/>.</param>
    /// <param name="textures">The service that resolves the image a sprite names, or null to draw only the handles a game set.</param>
    /// <param name="sheets">The service that resolves the frame a sprite names, or null to draw only whole images.</param>
    /// <param name="materials">The service that resolves the shader of a layer and sends its uniforms, or null to draw every layer with the program of the engine.</param>
    /// <param name="logger">The logger that reports a shader that cannot be loaded, or null to report nothing.</param>
    /// <remarks>A container passes all of the services, which is what lets a sprite of a prototype or a scene name its image, the part of it to draw and the material that draws a layer.</remarks>
    public RenderSystem(
        IRenderer renderer,
        SpriteSorter sorter,
        ITextureService? textures = null,
        ISpriteSheetService? sheets = null,
        IMaterialService? materials = null,
        ILogger<RenderSystem>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(sorter);
        _renderer = renderer;
        _sorter = sorter;
        _textures = textures;
        _sheets = sheets;
        _materials = materials;
        _logger = logger;
    }

    /// <inheritdoc />
    public void Render(World world, in Camera2D camera)
    {
        ArgumentNullException.ThrowIfNull(world);

        // A renderer that was attached to another window holds a device of its own, and every program compiled against the one
        // before it went away with it: the pass reads the attachment of the device as a frame opens and tells the materials to
        // let go of what they compiled, which is what keeps a handle of a window that is gone from being drawn with. This is the
        // one place a frame begins for the world, so nothing of a game has to remember the call.
        if (_generation != _renderer.DeviceGeneration)
        {
            _generation = _renderer.DeviceGeneration;
            _materials?.Build();
        }

        _renderer.SetCamera(camera);
        _renderer.BeginFrame(true);

        // The pass draws with the program of the engine until a layer names another one, whatever the pass before it left
        // behind, and the program of the engine is what it hands to the passes that follow it.
        _renderer.ResetShader();
        _currentShader = default;

        CollectSprites(world, camera);
        _sorter.Sort(_sprites, entity => world.Get<SpriteComponent>(entity).ZOrder);

        foreach (Entity entity in _sprites)
        {
            SpriteComponent sprite = world.Get<SpriteComponent>(entity);
            TransformComponent transform = world.Get<TransformComponent>(entity);

            if (sprite.Layers is { Length: > 0 } layers)
            {
                DrawLayers(layers, sprite, transform);
                continue;
            }

            SpriteRegion region = Region(world, entity, sprite);
            Vector2 size = (sprite.Size == Vector2.Zero ? region.Cell : sprite.Size) * transform.Scale;
            if (size == Vector2.Zero)
            {
                continue;
            }

            _renderer.DrawTextureRegion(region.Texture, region.Source, transform.Position, size, sprite.Color, transform.Rotation);
        }

        SetShader(default);
        _renderer.EndFrame();
    }

    /// <summary>Draws every layer of a sprite, in the order the document writes them.</summary>
    /// <param name="layers">The layers of the sprite.</param>
    /// <param name="sprite">The sprite, which is what the size and the colour of every layer are.</param>
    /// <param name="transform">The transform, which is where and how the layers are drawn.</param>
    /// <remarks>
    /// A layer is drawn over the one before it, at the box of the sprite. A sprite without a size draws nothing, exactly as a
    /// sprite of one image does, which is why a sprite that names layers sets that size.
    /// </remarks>
    private void DrawLayers(SpriteLayer[] layers, in SpriteComponent sprite, in TransformComponent transform)
    {
        Vector2 size = sprite.Size * transform.Scale;

        if (size == Vector2.Zero)
        {
            return;
        }

        foreach (SpriteLayer layer in layers)
        {
            ShaderHandle shader = _materials?.Shader(layer.Material) ?? default;
            SetShader(shader);

            if (_materials is not null)
            {
                _materials.SetShader(_renderer, shader, layer.Material);
            }

            _renderer.DrawSprite(Texture(layer), transform.Position, size, sprite.Color, transform.Rotation);
        }
    }

    /// <summary>Draws the following quads with a shader, and hands the program of the engine back when the handle is a default one.</summary>
    /// <param name="shader">The shader of the layer that is drawn next, or a default handle for the program of the engine.</param>
    /// <remarks>One draw call samples one program, so a switch ends a batch: the pass switches only when the layer that follows names another shader than the layer before it, which keeps the quads of one shader in one call.</remarks>
    private void SetShader(ShaderHandle shader)
    {
        if (shader == _currentShader)
        {
            return;
        }

        if (shader == default)
        {
            _renderer.ResetShader();
        }
        else
        {
            _renderer.UseShader(shader);
        }

        _currentShader = shader;
    }

    /// <summary>Returns the texture of a layer, resolving the image that the document named.</summary>
    /// <param name="layer">The layer that is being drawn.</param>
    /// <returns>The handle of the image, the placeholder when the image is not there, or a zero handle for a layer that names none.</returns>
    private TextureHandle Texture(in SpriteLayer layer) =>
        _textures is not null && layer.Image is string path ? _textures.Resolve(path) : default;

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
    /// The path of the component is what content says the image is, so it is resolved whenever it is set: the handle of the
    /// component is a copy of what the last frame resolved, and a game that changes the path of a sprite gets the image of the
    /// new one without clearing that copy itself. The service answers a path with what it already resolved, so the image is
    /// uploaded once however many frames ask for it, and the handle that was resolved is written back into the component for a
    /// game to read. A sprite without a path and without a handle is drawn as a solid color quad, which is what the renderer
    /// draws for a zero identifier, and one whose image is missing is drawn as the placeholder of the texture service.
    /// </remarks>
    private TextureHandle Texture(World world, Entity entity, in SpriteComponent sprite)
    {
        if (_textures is null || sprite.TexturePath is not string path)
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
