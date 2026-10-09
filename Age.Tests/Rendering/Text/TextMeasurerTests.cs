using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class TextMeasurerTests
{
    [Fact]
    public void BitmapTextMeasurer_EveryCharacter_IsOneCellWide()
    {
        BitmapTextMeasurer measurer = BitmapTextMeasurer.Instance;

        measurer.Measure("abc").Should().Be(new Vector2(24f, 8f));
        measurer.Measure("Привет").Should().Be(new Vector2(48f, 8f), "a character the built-in font has no glyph for is drawn as a space, which is a cell as well");
        measurer.Measure(string.Empty).Should().Be(new Vector2(0f, 8f));
        measurer.Metrics.Ascent.Should().Be(measurer.Metrics.LineHeight);
        measurer.Metrics.Descent.Should().Be(0f);
    }

    [Fact]
    public void StackedTextMeasurer_EveryCharacter_IsMeasuredByTheFirstFontThatCoversIt()
    {
        var fonts = new FakeFontService();
        var latin = new FontStyle { Path = "Fonts/Cousine-Regular.ttf", PixelHeight = 24f };
        var cyrillic = new FontStyle { Path = "Fonts/Cousine-Regular.ttf", PixelHeight = 24f, FirstCharacter = 'А', LastCharacter = 'я' };
        var measurer = new StackedTextMeasurer(fonts, [(latin, new FontHandle(default, 6)), (cyrillic, new FontHandle(default, 12))]);

        measurer.Measure("abЖ").Should().Be(new Vector2(24f, 6f), "two letters of the first font and one of the second");
        measurer.Metrics.LineHeight.Should().Be(6f, "the metrics of a stack are those of its first font");
    }

    [Fact]
    public void StackedTextMeasurer_CharacterThatNoFontCovers_IsMeasuredByTheFirstFont()
    {
        var fonts = new FakeFontService();
        var measurer = new StackedTextMeasurer(fonts, [(new FontStyle(), new FontHandle(default, 6))]);

        measurer.Measure("\u4e00").Should().Be(new Vector2(6f, 6f), "a character that no font of the stack covers is drawn as a space of the first font");
    }

    [Fact]
    public void StackedTextMeasurer_EmptyStack_IsRefused()
    {
        var fonts = new FakeFontService();
        Action build = () => _ = new StackedTextMeasurer(fonts, []);

        build.Should().Throw<ArgumentException>().WithParameterName("stack");
    }

    private sealed class FakeFontService : IFontService
    {
        public int Count => 0;

        public FontHandle Load(string relativePath, float pixelHeight, char first = ' ', char last = '~') => default;

        public bool IsAlive(FontHandle font) => true;

        public bool Unload(FontHandle font) => false;

        public void UnloadAll()
        {
        }

        public Vector2 Measure(FontHandle font, ReadOnlySpan<char> text) => new(text.Length * font.Atlas, font.Atlas);

        public FontMetrics Metrics(FontHandle font) => new(font.Atlas, font.Atlas);

        public void Draw(FontHandle font, ReadOnlySpan<char> text, Vector2 position, Color color)
        {
        }
    }
}
