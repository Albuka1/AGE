using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class GlyphPackingTests
{
    [Fact]
    public void GlyphPacking_Boxes_AreInsideTheAtlasAndDoNotOverlap()
    {
        Vector2[] sizes =
        [
            new(8f, 8f),
            new(3f, 11f),
            new(17f, 5f),
            new(1f, 1f),
            new(24f, 24f),
            new(6f, 6f),
            new(9f, 14f),
        ];
        var placements = new (int X, int Y)[sizes.Length];

        GlyphPacking.Place(sizes, placements, out int width, out int height);

        for (int index = 0; index < sizes.Length; index++)
        {
            (int x, int y) = placements[index];
            int cellWidth = (int)sizes[index].X + (2 * GlyphPacking.Padding);
            int cellHeight = (int)sizes[index].Y + (2 * GlyphPacking.Padding);

            x.Should().BeGreaterThanOrEqualTo(0);
            y.Should().BeGreaterThanOrEqualTo(GlyphPacking.Padding);
            (x + cellWidth).Should().BeLessThanOrEqualTo(width);
            (y + cellHeight).Should().BeLessThanOrEqualTo(height);

            for (int other = index + 1; other < sizes.Length; other++)
            {
                (int otherX, int otherY) = placements[other];
                int otherWidth = (int)sizes[other].X + (2 * GlyphPacking.Padding);
                int otherHeight = (int)sizes[other].Y + (2 * GlyphPacking.Padding);

                bool apart = x + cellWidth <= otherX
                    || otherX + otherWidth <= x
                    || y + cellHeight <= otherY
                    || otherY + otherHeight <= y;

                apart.Should().BeTrue($"{index} and {other} do not share a pixel");
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(40)]
    public void GlyphPacking_Atlas_IsAsWideAsAPowerOfTwoAndNotTallerThanWide(int glyphCount)
    {
        var sizes = new Vector2[glyphCount];
        for (int index = 0; index < glyphCount; index++)
        {
            sizes[index] = new Vector2(4f + index, 6f + (index % 3));
        }

        GlyphPacking.Place(sizes, new (int X, int Y)[glyphCount], out int width, out int height);

        width.Should().BeGreaterThanOrEqualTo(GlyphPacking.MinimumWidth);
        (width & (width - 1)).Should().Be(0, "the width is a power of two");
        height.Should().BeGreaterThanOrEqualTo(2 * GlyphPacking.Padding);
        height.Should().BeLessThanOrEqualTo(width);
    }

    [Fact]
    public void GlyphPacking_BoxWithAnUnsupportedDimension_Throws()
    {
        var placements = new (int X, int Y)[1];

        FluentActions.Invoking(() => GlyphPacking.Place([new Vector2(20000f, 10f)], placements, out _, out _))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(-4f)]
    public void GlyphPacking_BoxThatIsNotAFiniteSize_Throws(float value)
    {
        var placements = new (int X, int Y)[1];

        FluentActions.Invoking(() => GlyphPacking.Place([new Vector2(value, value)], placements, out _, out _))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GlyphPacking_BoxesThatDoNotFit_ThrowsInsteadOfGrowingForever()
    {
        var sizes = new Vector2[50];
        for (int index = 0; index < sizes.Length; index++)
        {
            sizes[index] = new Vector2(4000f, 4000f);
        }

        var placements = new (int X, int Y)[sizes.Length];

        FluentActions.Invoking(() => GlyphPacking.Place(sizes, placements, out _, out _))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GlyphPacking_TooFewPlacements_Throws() =>
        FluentActions.Invoking(() => GlyphPacking.Place([new Vector2(4f, 4f), new Vector2(4f, 4f)], new (int X, int Y)[1], out _, out _))
            .Should().Throw<ArgumentException>();
}
