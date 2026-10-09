using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Age.Tests;

public sealed class TextRenderSystemTests
{
    [Fact]
    public void TextRenderSystem_DrawsTheLinesInAscendingZOrder()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var fonts = new FakeFontService();
        var system = new TextRenderSystem(renderer, new SpriteSorter(), fonts);
        CreateLine(world, "second", zOrder: 2);
        CreateLine(world, "first", zOrder: 0);
        CreateLine(world, "third", zOrder: 1);

        system.Render(world, Camera());

        fonts.Drawn.Should().Equal("first", "third", "second");
        renderer.BuiltIn.Should().BeEmpty();
        renderer.Frames.Should().Be(1);
    }

    [Fact]
    public void TextRenderSystem_BakesTheFontOfALineForTheRangeItNames()
    {
        var world = new World();
        var fonts = new FakeFontService();
        var system = new TextRenderSystem(new RecordingRenderer(), new SpriteSorter(), fonts);
        Entity line = CreateLine(world, "Привет", zOrder: 0, last: '\u04FF');

        system.Render(world, Camera());

        fonts.Requests.Should().ContainSingle().Which.Should().Be(("Fonts/Cousine-Regular.ttf", 24f, ' ', '\u04FF'));
        world.Get<TextComponent>(line).Font.Atlas.Should().Be(7, "the handle that was baked is written back for a game to read");
    }

    [Fact]
    public void TextRenderSystem_LineThatLeavesTheRangeAtZero_IsBakedForThePrintableAsciiRange()
    {
        var world = new World();
        var fonts = new FakeFontService();
        var system = new TextRenderSystem(new RecordingRenderer(), new SpriteSorter(), fonts);
        CreateLine(world, "Hello", zOrder: 0);

        system.Render(world, Camera());

        fonts.Requests.Should().ContainSingle().Which.Should().Be(("Fonts/Cousine-Regular.ttf", 24f, ' ', '~'));
    }

    [Fact]
    public void TextRenderSystem_LineWithoutAFontPath_IsDrawnWithTheBuiltInFont()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var fonts = new FakeFontService();
        var system = new TextRenderSystem(renderer, new SpriteSorter(), fonts);
        CreateLine(world, "Hello AGE", zOrder: 0, fontPath: null);

        system.Render(world, Camera());

        renderer.BuiltIn.Should().Equal("Hello AGE");
        fonts.Drawn.Should().BeEmpty();
        fonts.Requests.Should().BeEmpty();
    }

    [Fact]
    public void TextRenderSystem_FontThatCannotBeBaked_FallsBackToTheBuiltInFontAndIsReportedOnce()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var fonts = new FakeFontService { Failure = new FileNotFoundException("There is no file at that path.") };
        var logger = new RecordingLogger();
        var system = new TextRenderSystem(renderer, new SpriteSorter(), fonts, logger);
        CreateLine(world, "Hello AGE", zOrder: 0);

        system.Render(world, Camera());
        system.Render(world, Camera());

        renderer.BuiltIn.Should().Equal("Hello AGE", "Hello AGE");
        fonts.Drawn.Should().BeEmpty();
        logger.Entries.Should().ContainSingle("one line per font rather than one per frame");
        logger.Entries[0].Should().Contain("Fonts/Cousine-Regular.ttf");
        logger.Entries[0].Should().Contain("There is no file at that path.");
    }

    [Fact]
    public void TextRenderSystem_LineThatSaysNothingOrHasNoPlace_IsNotDrawn()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var fonts = new FakeFontService();
        var system = new TextRenderSystem(renderer, new SpriteSorter(), fonts);
        Entity empty = CreateLine(world, string.Empty, zOrder: 0);
        CreateLine(world, "no height", zOrder: 0, height: 0f);
        Entity nowhere = world.CreateEntity();
        world.Set(nowhere, new TextComponent { Text = "no transform", FontPath = "Fonts/Cousine-Regular.ttf", PixelHeight = 24f });

        system.Render(world, Camera());

        fonts.Drawn.Should().BeEmpty();
        renderer.BuiltIn.Should().BeEmpty();
        renderer.Frames.Should().Be(1, "the frame is opened and closed even when nothing is drawn");
        world.IsAlive(empty).Should().BeTrue();
    }

    private static Entity CreateLine(World world, string text, int zOrder, float height = 24f, char last = '\0', string? fontPath = "Fonts/Cousine-Regular.ttf")
    {
        Entity entity = world.CreateEntity();
        world.Set(entity, new TransformComponent { Position = new Vector2(zOrder * 16f, 32f) });
        world.Set(entity, new TextComponent
        {
            Text = text,
            FontPath = fontPath,
            PixelHeight = height,
            LastCharacter = last,
            Color = Color.White,
            ZOrder = zOrder,
        });

        return entity;
    }

    private static Camera2D Camera() => new()
    {
        Position = Vector2.Zero,
        Zoom = 1f,
        ViewportSize = new Vector2(1280f, 720f),
    };

    private sealed class FakeFontService : IFontService
    {
        public List<(string Path, float Height, char First, char Last)> Requests { get; } = [];

        public List<string> Drawn { get; } = [];

        public Exception? Failure { get; init; }

        public int Count => 0;

        public FontHandle Load(string relativePath, float pixelHeight, char first = ' ', char last = '~')
        {
            Requests.Add((relativePath, pixelHeight, first, last));

            return Failure is null ? new FontHandle(default, 7) : throw Failure;
        }

        public bool IsAlive(FontHandle font) => font.Atlas == 7;

        public bool Unload(FontHandle font) => false;

        public void UnloadAll()
        {
        }

        public Vector2 Measure(FontHandle font, ReadOnlySpan<char> text) => Vector2.Zero;

        public void Draw(FontHandle font, ReadOnlySpan<char> text, Vector2 position, Color color) => Drawn.Add(new string(text));
    }

    private sealed class RecordingRenderer : IRenderer
    {
        public List<string> BuiltIn { get; } = [];

        public int Frames { get; private set; }

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

        public void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color, float rotation = 0f)
        {
        }

        public void DrawTextureRegion(TextureHandle texture, Rect source, Vector2 position, Vector2 size, Color color, float rotation = 0f)
        {
        }

        public void DrawRectangle(Rect rect, Color color)
        {
        }

        public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color) => BuiltIn.Add(new string(text));

        public void EndFrame() => Frames++;

        public TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height) => new(1);

        public void ReleaseTexture(TextureHandle texture)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger : ILogger<TextRenderSystem>
    {
        public List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(formatter(state, exception));
    }
}
