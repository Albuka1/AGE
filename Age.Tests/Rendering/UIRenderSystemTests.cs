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

    [Fact]
    public void UIRenderSystem_APanelOfNoAlpha_IsNotDrawn()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new UIRenderSystem(renderer, new SpriteSorter(), new TextRenderer(renderer));
        Entity panel = world.CreateEntity();
        world.Set(panel, new RectTransformComponent { Position = Vector2.Zero, Size = new Vector2(100f, 50f), Visible = true });
        world.Set(panel, new PanelComponent { Color = new Color(0, 0, 0) { A = 0 } });

        system.Render(world, Camera());

        renderer.Rectangles.Should().BeEmpty("a rectangle of no alpha is nothing a person sees, which is what a zone is");
    }

    [Fact]
    public void UIRenderSystem_AButtonOfNoAlpha_IsNotDrawn()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new UIRenderSystem(renderer, new SpriteSorter(), new TextRenderer(renderer));
        Entity zone = world.CreateEntity();
        world.Set(zone, new RectTransformComponent { Position = Vector2.Zero, Size = new Vector2(100f, 100f), Visible = true });
        world.Set(zone, new ButtonComponent
        {
            BaseColor = new Color(0, 0, 0) { A = 0 },
            BorderColor = new Color(0, 0, 0) { A = 0 },
            BorderThickness = 0,
            Interactable = true,
        });

        system.Render(world, Camera());

        renderer.Rectangles.Should().BeEmpty("the face of a zone and the frame of it are both nothing to draw");
        renderer.BuiltIn.Should().BeEmpty();
    }

    [Fact]
    public void UIRenderSystem_APanelWithAFrameAndNoFill_DrawsTheFrameAlone()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new UIRenderSystem(renderer, new SpriteSorter(), new TextRenderer(renderer));
        Entity panel = world.CreateEntity();
        world.Set(panel, new RectTransformComponent { Position = Vector2.Zero, Size = new Vector2(100f, 50f), Visible = true });
        world.Set(panel, new PanelComponent
        {
            Color = new Color(0, 0, 0) { A = 0 },
            BorderColor = Color.White,
            BorderThickness = 1,
        });

        system.Render(world, Camera());

        // The fill is skipped, and the frame is what is left: a panel that draws an edge over the picture under it, which is
        // what a slot of a card that is drawn by the game is.
        renderer.Rectangles.Should().HaveCount(4, "a frame is four rectangles and there is no fill behind them");
    }

    [Fact]
    public void UIRenderSystem_AButtonOfNoAlphaWithAFrame_DrawsTheFrameAlone()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new UIRenderSystem(renderer, new SpriteSorter(), new TextRenderer(renderer));
        Entity slot = world.CreateEntity();
        world.Set(slot, new RectTransformComponent { Position = Vector2.Zero, Size = new Vector2(64f, 64f), Visible = true });
        world.Set(slot, new ButtonComponent
        {
            BaseColor = new Color(0, 0, 0) { A = 0 },
            BorderColor = Color.White,
            BorderThickness = 2,
            Interactable = true,
        });

        system.Render(world, Camera());

        // A slot of a card is a zone that says where its edge is: the face of it draws nothing, and what a person sees of the
        // slot is the frame over the picture under it.
        renderer.Rectangles.Should().HaveCount(4);
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
