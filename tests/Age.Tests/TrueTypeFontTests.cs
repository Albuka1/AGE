using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class TrueTypeFontTests
{
    [Fact]
    public void TrueTypeFontBake_AsciiRange_ProducesAnAtlasWithGlyphMetrics()
    {
        FontAtlas atlas = TrueTypeFontBake.Bake(ShippedFont(), 24f, TrueTypeFontBake.AsciiCharacters);

        atlas.First.Should().Be(' ');
        atlas.Glyphs.Should().HaveCount(TrueTypeFontBake.AsciiCharacters.Length);
        atlas.Width.Should().BeGreaterThan(0);
        (atlas.Width & (atlas.Width - 1)).Should().Be(0, "the atlas starts as a power of two");
        atlas.Pixels.Should().HaveCount(atlas.Width * atlas.Height * 4);
        atlas.Pixels.Should().Contain(byte.MaxValue, "glyphs are drawn as white coverage");
        atlas.Ascent.Should().BeGreaterThan(0f);
        atlas.LineHeight.Should().BeGreaterThan(atlas.Ascent - 0.001f);

        FontGlyph wide = atlas.Glyphs[TrueTypeFontBake.AsciiCharacters.IndexOf('M')];
        FontGlyph narrow = atlas.Glyphs[TrueTypeFontBake.AsciiCharacters.IndexOf('i')];

        // The shipped font is monospaced, so both letters share an advance and a box. What is checked here is that every
        // glyph was measured and placed, and that the box of a letter has ink.
        wide.Advance.Should().BeGreaterThan(0f);
        narrow.Advance.Should().BeGreaterThan(0f);
        wide.Size.X.Should().BeGreaterThan(0f);
        wide.Size.Y.Should().BeGreaterThan(0f);
        narrow.Size.Y.Should().BeGreaterThan(0f, "the dot of the i is part of the glyph");
        wide.Bearing.Y.Should().BeLessThanOrEqualTo(0f, "a glyph sits on the baseline or above it");
        wide.Source.X.Should().BeInRange(0f, 1f);
        wide.Source.Width.Should().BeInRange(0f, 1f);
    }

    [Fact]
    public void TrueTypeFontBake_LargerHeight_ProducesLargerGlyphs()
    {
        byte[] font = ShippedFont();

        FontAtlas small = TrueTypeFontBake.Bake(font, 12f, "ABC");
        FontAtlas large = TrueTypeFontBake.Bake(font, 48f, "ABC");

        large.Glyphs[1].Size.X.Should().BeGreaterThan(small.Glyphs[1].Size.X);
        large.LineHeight.Should().BeGreaterThan(small.LineHeight);
    }

    [Fact]
    public void TrueTypeFontBake_RangeThatIsNotContiguous_Throws()
    {
        byte[] font = ShippedFont();

        FluentActions.Invoking(() => TrueTypeFontBake.Bake(font, 16f, "ABD"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TrueTypeFontBake_NoCharacterAtAll_Throws()
    {
        byte[] font = ShippedFont();

        FluentActions.Invoking(() => TrueTypeFontBake.Bake(font, 16f, string.Empty))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TrueTypeFontBake_BytesThatHoldNoFont_Throws() =>
        FluentActions.Invoking(() => TrueTypeFontBake.Bake(new byte[512], 16f, TrueTypeFontBake.AsciiCharacters))
            .Should().Throw<ArgumentException>();

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void TrueTypeFontBake_HeightThatIsNotPositive_Throws(float pixelHeight)
    {
        byte[] font = ShippedFont();

        FluentActions.Invoking(() => TrueTypeFontBake.Bake(font, pixelHeight, TrueTypeFontBake.AsciiCharacters))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TrueTypeFontBake_HeightTooLargeForTheAtlas_Throws()
    {
        byte[] font = ShippedFont();

        FluentActions.Invoking(() => TrueTypeFontBake.Bake(font, 100000f, "A"))
            .Should().Throw<ArgumentOutOfRangeException>("a glyph of that size does not fit in a supported atlas");
    }

    [Fact]
    public void TrueTypeFontTests_ShippedFont_IsATrueTypeFile()
    {
        byte[] font = ShippedFont();

        font.Should().HaveCountGreaterThan(1000);
        font[0].Should().Be(0, "a TrueType file starts with 0x00010000");
        font[1].Should().Be(1);
        font[2].Should().Be(0);
        font[3].Should().Be(0);
    }

    /// <summary>Reads the font that the repository ships with the sample, which the tests bake instead of a system font.</summary>
    private static byte[] ShippedFont() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fonts", "Cousine-Regular.ttf"));
}
