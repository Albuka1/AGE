using Age.Core;
using Age.Rendering;
using Age.UI;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Age.Tests;

public sealed class UIRenderSystemTests
{
    [Fact]
    public void UIRenderSystem_LabelWithoutAFont_IsDrawnWithTheBuiltInFont()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var fonts = new FakeFontService();
        var system = new UIRenderSystem(renderer, new SpriteSorter(), fonts);
        CreateLabel(world, "Hello AGE");

        system.Render(world, Camera());

        renderer.BuiltIn.Should().Equal("Hello AGE");
        fonts.Drawn.Should().BeEmpty();
        renderer.Frames.Should().Be(1);
    }

    [Fact]
    public void UIRenderSystem_LabelThatNamesAFont_IsDrawnWithItForTheRangeItCovers()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var fonts = new FakeFontService();
        var system = new UIRenderSystem(renderer, new SpriteSorter(), fonts);
        CreateLabel(world, "3 СЃСѓС‰РЅРѕСЃС‚Рё", font: true);

        system.Render(world, Camera());

        fonts.Requests.Should().ContainSingle().Which.Should().Be(("Fonts/Cousine-Regular.ttf", 16f, ' ', '\u04FF'));
        fonts.Drawn.Should().Equal("3 СЃСѓС‰РЅРѕСЃС‚Рё");
        renderer.BuiltIn.Should().BeEmpty();
    }

    [Fact]
    public void UIRenderSystem_LabelWhoseFontCannotBeBaked_IsDrawnWithTheBuiltInFontAndReportedOnce()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var fonts = new FakeFontService { Failure = new UnauthorizedAccessException("the font cannot be read") };
        var logger = new RecordingLogger();
        var system = new UIRenderSystem(renderer, new SpriteSorter(), fonts, logger);
        CreateLabel(world, "Hello AGE", font: true);

        system.Render(world, Camera());
        system.Render(world, Camera());

        renderer.BuiltIn.Should().Equal("Hello AGE", "Hello AGE");
        fonts.Drawn.Should().BeEmpty();
        logger.Entries.Should().ContainSingle("one line per font rather than one per frame");
    }

    private static void CreateLabel(World world, string text, bool font = false)
    {
        Entity entity = world.CreateEntity();
        world.Set(entity, new RectTransformComponent { Position = new Vector2(100f, 100f), Size = new Vector2(200f, 50f), Visible = true });
        world.Set(entity, new TextLabelComponent
        {
            Text = text,
            Color = Color.White,
            FontPath = font ? "Fonts/Cousine-Regular.ttf" : null,
            PixelHeight = font ? 16f : 0f,
            LastCharacter = font ? '\u04FF' : '\0',
        });
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

        public FontMetrics Metrics(FontHandle font) => default;

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

    private sealed class RecordingLogger : ILogger<UIRenderSystem>
    {
        public List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(formatter(state, exception));
    }
}
