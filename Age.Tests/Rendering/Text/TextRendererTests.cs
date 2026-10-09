using Age.Core;
using Age.Rendering;
using FluentAssertions;
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
