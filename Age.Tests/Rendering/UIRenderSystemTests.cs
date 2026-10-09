using Age.Core;
using Age.Rendering;
using Age.UI;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class UIRenderSystemTests
{
    [Fact]
    public void UIRenderSystem_DrawsALabelInTheBoxOfItsRectangle()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new UIRenderSystem(renderer, new SpriteSorter(), new TextRenderer(renderer));
        CreateLabel(world, "Hello AGE", size: new Vector2(200f, 50f));

        system.Render(world, Camera());

        renderer.BuiltIn.Should().Equal("Hello AGE");
    }

    [Fact]
    public void UIRenderSystem_LabelIsLaidOutIntoTheSizeOfTheRectangle()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new UIRenderSystem(renderer, new SpriteSorter(), new TextRenderer(renderer));
        var style = new TextStyle { Wrap = TextWrap.None, Overflow = TextOverflow.Clip };
        CreateLabel(world, "one\ntwo", size: new Vector2(200f, 10f), style: style);

        system.Render(world, Camera());

        renderer.BuiltIn.Should().Equal("one");
    }

    [Fact]
    public void UIRenderSystem_BoxOfTheText_WinsOverTheSizeOfTheRectangle()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new UIRenderSystem(renderer, new SpriteSorter(), new TextRenderer(renderer));
        CreateLabel(world, "aaaa bbbb cccc", size: new Vector2(400f, 50f), box: new Vector2(60f, 0f));

        system.Render(world, Camera());

        renderer.BuiltIn.Should().Equal("aaaa", "bbbb", "cccc");
    }

    [Fact]
    public void UIRenderSystem_ButtonWithAText_DrawsTheRectangleAndTheLabel()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new UIRenderSystem(renderer, new SpriteSorter(), new TextRenderer(renderer));
        Entity entity = CreateLabel(world, "Hello AGE", size: new Vector2(200f, 50f));
        world.Set(entity, new ButtonComponent { BaseColor = Color.Blue, Interactable = true });

        system.Render(world, Camera());

        renderer.Rectangles.Should().ContainSingle().Which.Size.Should().Be(new Vector2(200f, 50f));
        renderer.BuiltIn.Should().Equal("Hello AGE");
    }

    [Fact]
    public void UIRenderSystem_InvisibleElement_IsNotDrawn()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new UIRenderSystem(renderer, new SpriteSorter(), new TextRenderer(renderer));
        CreateLabel(world, "Hello AGE", size: new Vector2(200f, 50f), visible: false);

        system.Render(world, Camera());

        renderer.BuiltIn.Should().BeEmpty();
        renderer.Rectangles.Should().BeEmpty();
    }

    private static Entity CreateLabel(World world, string text, Vector2 size, TextStyle style = default, Vector2 box = default, bool visible = true)
    {
        Entity entity = world.CreateEntity();
        world.Set(entity, new RectTransformComponent { Position = new Vector2(10f, 20f), Size = size, Visible = visible });
        world.Set(entity, new TextComponent { Text = text, Style = style, Box = box });

        return entity;
    }

    private static Camera2D Camera() => new()
    {
        Position = Vector2.Zero,
        Zoom = 1f,
        ViewportSize = new Vector2(1280f, 720f),
    };

    private sealed class RecordingRenderer : IRenderer
    {
        public List<string> BuiltIn { get; } = [];

        public List<Rect> Rectangles { get; } = [];

        public Vector2 ViewportSize => new(1280f, 720f);

        public void Attach(IWindowService window) { }

        public void SetCamera(Camera2D camera) { }

        public void BeginFrame(bool clear) { }

        public void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color, float rotation = 0f) { }

        public void DrawTextureRegion(TextureHandle texture, Rect source, Vector2 position, Vector2 size, Color color, float rotation = 0f) { }

        public void DrawRectangle(Rect rect, Color color) => Rectangles.Add(rect);

        public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color) => BuiltIn.Add(new string(text));

        public void EndFrame() { }

        public TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height) => new(1);

        public void ReleaseTexture(TextureHandle texture) { }

        public void Dispose() { }
    }
}
