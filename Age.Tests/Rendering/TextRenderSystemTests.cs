using Age.Core;
using Age.Rendering;
using Age.UI;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class TextRenderSystemTests
{
    [Fact]
    public void TextRenderSystem_DrawsTheLinesOfAWorldInAscendingZOrder()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new TextRenderSystem(renderer, new SpriteSorter(), new TextRenderer(renderer));
        CreateLine(world, "second", zOrder: 2);
        CreateLine(world, "first", zOrder: 0);
        CreateLine(world, "third", zOrder: 1);

        system.Render(world, Camera());

        renderer.BuiltIn.Should().Equal("first", "third", "second");
    }

    [Fact]
    public void TextRenderSystem_TextThatNamesAKey_IsDrawnAsWhatTheStringsSay()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new TextRenderSystem(renderer, new SpriteSorter(), new TextRenderer(renderer, null, new FakeTextSource()));
        Entity entity = CreateLine(world, string.Empty, zOrder: 0, key: "ent-Goblin");

        system.Render(world, Camera());

        renderer.BuiltIn.Should().Equal("a goblin");
        world.Get<TextComponent>(entity).MeasuredSize.Should().Be(new Vector2(8f * "a goblin".Length, 8f), "the built-in font measures one cell per character");
    }

    [Fact]
    public void TextRenderSystem_TextWithARectangle_IsLeftToThePassOfTheInterface()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new TextRenderSystem(renderer, new SpriteSorter(), new TextRenderer(renderer));
        Entity entity = world.CreateEntity();
        world.Set(entity, new TransformComponent());
        world.Set(entity, new RectTransformComponent { Size = new Vector2(100f, 20f), Visible = true });
        world.Set(entity, new TextComponent { Text = "Hello AGE" });

        system.Render(world, Camera());

        renderer.BuiltIn.Should().BeEmpty();
    }

    [Fact]
    public void TextRenderSystem_TextThatSaysNothing_IsNotDrawn()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new TextRenderSystem(renderer, new SpriteSorter(), new TextRenderer(renderer));
        Entity empty = world.CreateEntity();
        world.Set(empty, new TransformComponent());
        world.Set(empty, new TextComponent());
        CreateLine(world, "one", zOrder: 1);

        system.Render(world, Camera());

        renderer.BuiltIn.Should().Equal("one");
    }

    [Fact]
    public void TextRenderSystem_TextOfAFontThatCannotBeBaked_FallsBackToTheBuiltInFont()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var fonts = new FailingFontService();
        var style = new TextStyle { Fonts = [new FontStyle { Path = "Fonts/Missing.ttf", PixelHeight = 24f }] };
        var system = new TextRenderSystem(renderer, new SpriteSorter(), new TextRenderer(renderer, fonts));
        CreateLine(world, "Hello AGE", zOrder: 0, style: style);

        system.Render(world, Camera());

        renderer.BuiltIn.Should().Equal("Hello AGE");
        fonts.Requests.Should().ContainSingle().Which.Should().Be("Fonts/Missing.ttf");
    }

    private static Entity CreateLine(World world, string text, int zOrder, string? key = null, TextStyle style = default)
    {
        Entity entity = world.CreateEntity();
        world.Set(entity, new TransformComponent { Position = new Vector2(0f, zOrder * 16f) });
        world.Set(entity, new TextComponent { Text = text, Key = key, Style = style, ZOrder = zOrder });

        return entity;
    }

    private static Camera2D Camera() => new()
    {
        Position = Vector2.Zero,
        Zoom = 1f,
        ViewportSize = new Vector2(1280f, 720f),
    };

    private sealed class FakeTextSource : ITextSource
    {
        public string Language => "en";

        public string Resolve(string key, params (string Name, object? Value)[] arguments) => "a goblin";
    }

    private sealed class FailingFontService : IFontService
    {
        public List<string> Requests { get; } = [];

        public int Count => 0;

        public FontHandle Load(string relativePath, float pixelHeight, char first = ' ', char last = '~')
        {
            Requests.Add(relativePath);

            throw new FileNotFoundException("There is no file at that path.");
        }

        public bool IsAlive(FontHandle font) => false;

        public bool Unload(FontHandle font) => false;

        public void UnloadAll() { }

        public Vector2 Measure(FontHandle font, ReadOnlySpan<char> text) => Vector2.Zero;

        public FontMetrics Metrics(FontHandle font) => default;

        public void Draw(FontHandle font, ReadOnlySpan<char> text, Vector2 position, Color color) { }
    }

    private sealed class RecordingRenderer : IRenderer
    {
        public List<string> BuiltIn { get; } = [];

        public Vector2 ViewportSize => new(1280f, 720f);

        public void Attach(IWindowService window) { }

        public void SetCamera(Camera2D camera) { }

        public void BeginFrame(bool clear) { }

        public void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color, float rotation = 0f) { }

        public void DrawTextureRegion(TextureHandle texture, Rect source, Vector2 position, Vector2 size, Color color, float rotation = 0f) { }

        public void DrawRectangle(Rect rect, Color color) { }

        public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color) => BuiltIn.Add(new string(text));

        public void EndFrame() { }

        public TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height) => new(1);

        public void ReleaseTexture(TextureHandle texture) { }

        public void Dispose() { }
    }
}
