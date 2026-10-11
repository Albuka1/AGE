using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Age.Tests;

public sealed class TextRendererTests
{
    [Fact]
    public void TextRenderer_LineThatNoEntityCarries_IsDrawnWithTheFontOfTheEngine()
    {
        var renderer = new RecordingRenderer();
        var fonts = new FakeFontService();
        var text = new TextRenderer(renderer, fonts, null, null, TextDefaults.Fonts);

        Vector2 size = text.Draw("Привет", new Vector2(8f, 8f), new TextStyle { Color = Color.White });

        fonts.Drawn.Should().Equal("Привет");
        renderer.BuiltIn.Should().BeEmpty("the engine ships a font, so the built-in one is not what draws a line of a game");
        size.Should().Be(new Vector2(6f * 8f, 16f));
        text.LineHeight.Should().Be(16f);
    }

    [Fact]
    public void TextRenderer_LineWithoutText_TakesNoRoom()
    {
        var renderer = new RecordingRenderer();
        var text = new TextRenderer(renderer, new FakeFontService(), null, null, TextDefaults.Fonts);

        text.Draw(null, Vector2.Zero, default).Should().Be(Vector2.Zero);
        text.Draw(string.Empty, Vector2.Zero, default).Should().Be(Vector2.Zero);
        renderer.BuiltIn.Should().BeEmpty();
    }

    [Fact]
    public void TextRenderer_LineWithoutAFontService_IsDrawnWithTheBuiltInFont()
    {
        var renderer = new RecordingRenderer();
        var text = new TextRenderer(renderer);

        text.Draw("Hello AGE", new Vector2(8f, 8f), new TextStyle()).Should().Be(new Vector2(9f * 8f, 8f));
        renderer.BuiltIn.Should().Equal("Hello AGE");
        text.LineHeight.Should().Be(8f);
    }

    [Fact]
    public void TextRenderer_LineHeight_AnswersTheBuiltInFontWhileAFontIsNotReadyAndTheRealOneAfterward()
    {
        // An overlay asks for the height of a line while it is built, which is before the asset loader of a game was given its root,
        // so the fonts it names cannot be baked yet: the read answers the built-in height and reports nothing, because the fonts are
        // not faulty, they are simply not loadable yet. Once they are, the height is the real one and stays that way.
        var renderer = new RecordingRenderer();
        var fonts = new ReadyFontService { Ready = false };
        var logger = new RecordingLogger<TextRenderer>();
        var text = new TextRenderer(renderer, fonts, null, logger, TextDefaults.Fonts);

        text.LineHeight.Should().Be(BitmapFontMetrics.GlyphHeight, "a font that is not loadable yet is not a font that is faulty");
        logger.Errors.Should().BeEmpty("reading the height before the fonts are there is not a mistake worth reporting");

        fonts.Ready = true;

        text.LineHeight.Should().Be(16f, "the read after the fonts are there bakes them and answers their height");
        text.LineHeight.Should().Be(16f, "the height is remembered once the fonts are there");
    }

    [Fact]
    public void TextRenderer_LineHeight_StaysSilentAndADrawReportsTheFont()
    {
        // The height is read while an overlay is built, which is before the game can say whether a font it names is faulty or simply
        // not loadable yet, so the read never reports: it answers the built-in height. The draw that follows, once the game is ready,
        // is where a font that really is faulty is reported, so nothing about the mistake is lost.
        var renderer = new RecordingRenderer();
        var logger = new RecordingLogger<TextRenderer>();
        var text = new TextRenderer(renderer, new BrokenFontService(), null, logger, TextDefaults.Fonts);

        text.LineHeight.Should().Be(BitmapFontMetrics.GlyphHeight, "a font that cannot be baked leaves the built-in font");
        logger.Errors.Should().BeEmpty("a read of the height never reports, because it cannot tell a fault from a font that is not ready");

        text.Draw("Hello", Vector2.Zero, new TextStyle());

        logger.Errors.Should().ContainSingle("the draw that needs the font is where a faulty one is reported");
        renderer.BuiltIn.Should().Equal(new[] { "Hello" }, "the characters of a font that cannot be baked fall to the built-in font");
    }

    private sealed class FakeFontService : IFontService
    {
        public List<string> Drawn { get; } = [];

        public int Count => 0;

        public FontHandle Load(string relativePath, float pixelHeight, char first = ' ', char last = '~') => new(default, 7);

        public bool IsAlive(FontHandle font) => font.Atlas == 7;

        public bool Unload(FontHandle font) => false;

        public void UnloadAll()
        {
        }

        public Vector2 Measure(FontHandle font, ReadOnlySpan<char> text) => new(text.Length * 8f, 16f);

        public FontMetrics Metrics(FontHandle font) => new(12f, 16f);

        public void Draw(FontHandle font, ReadOnlySpan<char> text, Vector2 position, Color color) => Drawn.Add(new string(text));
    }

    /// <summary>A font service that cannot bake a font until <see cref="Ready"/> is set, which is what a loader that was not initialized yet looks like.</summary>
    private sealed class ReadyFontService : IFontService
    {
        public bool Ready { get; set; }

        public int Count => 0;

        public FontHandle Load(string relativePath, float pixelHeight, char first = ' ', char last = '~') =>
            Ready ? new FontHandle(default, 7) : throw new InvalidOperationException("The asset loader has not been initialized. Call Initialize first.");

        public bool IsAlive(FontHandle font) => font.Atlas == 7;

        public bool Unload(FontHandle font) => false;

        public void UnloadAll()
        {
        }

        public Vector2 Measure(FontHandle font, ReadOnlySpan<char> text) => new(text.Length * 8f, 16f);

        public FontMetrics Metrics(FontHandle font) => new(12f, 16f);

        public void Draw(FontHandle font, ReadOnlySpan<char> text, Vector2 position, Color color)
        {
        }
    }

    /// <summary>A font service whose fonts can never be baked, which is what a font that is faulty looks like.</summary>
    private sealed class BrokenFontService : IFontService
    {
        public int Count => 0;

        public FontHandle Load(string relativePath, float pixelHeight, char first = ' ', char last = '~') =>
            throw new IOException($"The font '{relativePath}' is not a font.");

        public bool IsAlive(FontHandle font) => false;

        public bool Unload(FontHandle font) => false;

        public void UnloadAll()
        {
        }

        public Vector2 Measure(FontHandle font, ReadOnlySpan<char> text) => Vector2.Zero;

        public FontMetrics Metrics(FontHandle font) => default;

        public void Draw(FontHandle font, ReadOnlySpan<char> text, Vector2 position, Color color)
        {
        }
    }

    /// <summary>A logger that keeps what was written to it, which is how a test reads that a mistake was reported once.</summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Records { get; } = [];

        public IEnumerable<string> Errors => Records.Where(record => record.Level == LogLevel.Error).Select(record => record.Message);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Records.Add((logLevel, formatter(state, exception)));
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
