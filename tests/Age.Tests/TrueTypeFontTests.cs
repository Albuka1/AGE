using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class TrueTypeFontTests
{
    [Fact]
    public void TrueTypeFontBake_AsciiRange_ProducesAnAtlasWithGlyphMetrics()
    {
        FontAtlas atlas = TrueTypeFontBake.Bake(SystemFont(), 24f, TrueTypeFontBake.AsciiCharacters);

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

        wide.Advance.Should().BeGreaterThan(narrow.Advance);
        wide.Size.X.Should().BeGreaterThan(narrow.Size.X);
        narrow.Size.Y.Should().BeGreaterThan(0f, "the dot of the i is part of the glyph");
        wide.Bearing.Y.Should().BeLessThanOrEqualTo(0f, "a glyph sits on the baseline or above it");
        wide.Source.X.Should().BeInRange(0f, 1f);
        wide.Source.Width.Should().BeInRange(0f, 1f);
    }

    [Fact]
    public void TrueTypeFontBake_LargerHeight_ProducesLargerGlyphs()
    {
        byte[] font = SystemFont();

        FontAtlas small = TrueTypeFontBake.Bake(font, 12f, "ABC");
        FontAtlas large = TrueTypeFontBake.Bake(font, 48f, "ABC");

        large.Glyphs[1].Size.X.Should().BeGreaterThan(small.Glyphs[1].Size.X);
        large.LineHeight.Should().BeGreaterThan(small.LineHeight);
    }

    [Fact]
    public void TrueTypeFontBake_RangeThatIsNotContiguous_Throws()
    {
        byte[] font = SystemFont();

        FluentActions.Invoking(() => TrueTypeFontBake.Bake(font, 16f, "ABD"))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TrueTypeFontBake_NoCharacterAtAll_Throws()
    {
        byte[] font = SystemFont();

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
        byte[] font = SystemFont();

        FluentActions.Invoking(() => TrueTypeFontBake.Bake(font, pixelHeight, TrueTypeFontBake.AsciiCharacters))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>Reads a font of the operating system, or skips the test when the machine has none.</summary>
    private static byte[] SystemFont()
    {
        string folder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

        foreach (string name in new[] { "segoeui.ttf", "arial.ttf", "DejaVuSans.ttf", "LiberationSans-Regular.ttf" })
        {
            string path = Path.Combine(folder, name);
            if (File.Exists(path))
            {
                return File.ReadAllBytes(path);
            }
        }

        Assert.Skip("No TrueType font was found in the font folder of this machine.");
        return [];
    }
}
