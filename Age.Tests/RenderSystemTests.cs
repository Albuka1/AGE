using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class RenderSystemTests
{
    [Fact]
    public void RenderSystem_SpriteOutsideTheCamera_IsNotDrawn()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new RenderSystem(renderer, new SpriteSorter());
        CreateSprite(world, new Vector2(4000f, 4000f));

        system.Render(world, Camera(1280f, 720f));

        renderer.Starts.Should().BeEmpty();
    }

    [Fact]
    public void RenderSystem_DrawsTheVisibleSpritesInAscendingZOrder()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new RenderSystem(renderer, new SpriteSorter());
        CreateSprite(world, new Vector2(100f, 100f), zOrder: 2);
        CreateSprite(world, new Vector2(200f, 200f), zOrder: 0);
        CreateSprite(world, new Vector2(6000f, 6000f), zOrder: 1);

        system.Render(world, Camera(1280f, 720f));

        renderer.Starts.Should().Equal(new Vector2(200f, 200f), new Vector2(100f, 100f));
    }

    [Fact]
    public void RenderSystem_SpriteThatOnlyPartlyOverlapsTheCamera_IsDrawn()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new RenderSystem(renderer, new SpriteSorter());
        CreateSprite(world, new Vector2(-32f, -32f));

        system.Render(world, Camera(1280f, 720f));

        renderer.Starts.Should().ContainSingle();
    }

    [Fact]
    public void RenderSystem_UsesTheBoxOfTheRotatedQuad()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new RenderSystem(renderer, new SpriteSorter());

        // A long sprite whose centre is just outside the right edge of the camera: upright it reaches into the frame.
        CreateSprite(world, new Vector2(1100f, 355f), size: new Vector2(400f, 10f));
        system.Render(world, Camera(1280f, 720f));
        renderer.Starts.Should().ContainSingle();

        // The same sprite, turned a quarter around its own centre, is a thin bar outside of the frame.
        var turnedWorld = new World();
        var turned = new RecordingRenderer();
        CreateSprite(turnedWorld, new Vector2(1100f, 355f), size: new Vector2(400f, 10f), rotation: MathF.PI / 2f);
        new RenderSystem(turned, new SpriteSorter()).Render(turnedWorld, Camera(1280f, 720f));

        turned.Starts.Should().BeEmpty();
    }

    [Fact]
    public void RenderSystem_CameraWithoutAViewportSize_DrawsEverything()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new RenderSystem(renderer, new SpriteSorter());
        CreateSprite(world, new Vector2(4000f, 4000f));
        CreateSprite(world, new Vector2(-9000f, -9000f));

        system.Render(world, new Camera2D { Zoom = 1f });

        renderer.Starts.Should().HaveCount(2);
    }

    [Fact]
    public void RenderSystem_CameraWithALargerZoom_SeesMoreOfTheWorld()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new RenderSystem(renderer, new SpriteSorter());
        CreateSprite(world, new Vector2(4000f, 100f));

        system.Render(world, Camera(1280f, 720f));
        renderer.Starts.Should().BeEmpty();

        system.Render(world, Camera(1280f, 720f, zoom: 4f));

        renderer.Starts.Should().ContainSingle();
    }

    private static Camera2D Camera(float width, float height, float zoom = 1f) => new()
    {
        Position = Vector2.Zero,
        Zoom = zoom,
        ViewportSize = new Vector2(width, height),
    };

    private static Entity CreateSprite(
        World world,
        Vector2 position,
        int zOrder = 0,
        Vector2? size = null,
        float rotation = 0f)
    {
        Entity entity = world.CreateEntity();
        world.Set(entity, new TransformComponent { Position = position, Scale = new Vector2(1f, 1f), Rotation = rotation });
        world.Set(entity, new SpriteComponent
        {
            Size = size ?? new Vector2(64f, 64f),
            Color = Color.White,
            ZOrder = zOrder,
        });

        return entity;
    }

    private sealed class RecordingRenderer : IRenderer
    {
        public List<Vector2> Starts { get; } = [];

        public Vector2 ViewportSize => new(1280f, 720f);

        public void Attach(IWindowService window)
        {
        }

        public void SetCamera(Camera2D camera)
        {
        }

        public void BeginFrame(bool clear)
        {
        }

        public void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color, float rotation = 0f) =>
            Starts.Add(position);

        public void DrawTextureRegion(TextureHandle texture, Rect source, Vector2 position, Vector2 size, Color color, float rotation = 0f) =>
            DrawSprite(texture, position, size, color, rotation);

        public void DrawRectangle(Rect rect, Color color)
        {
        }

        public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color)
        {
        }

        public void EndFrame()
        {
        }

        public TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height) => default;

        public void ReleaseTexture(TextureHandle texture)
        {
        }

        public void Dispose()
        {
        }
    }
}
