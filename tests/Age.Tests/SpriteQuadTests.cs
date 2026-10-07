using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class SpriteQuadTests
{
    [Fact]
    public void SpriteQuad_WithoutRotation_ReturnsTheCornersOfTheRectangle()
    {
        var corners = new Vector2[4];

        SpriteQuad.Corners(new Vector2(10f, 20f), new Vector2(30f, 40f), 0f, corners);

        corners[0].Should().Be(new Vector2(10f, 20f));
        corners[1].Should().Be(new Vector2(40f, 20f));
        corners[2].Should().Be(new Vector2(10f, 60f));
        corners[3].Should().Be(new Vector2(40f, 60f));
    }

    [Fact]
    public void SpriteQuad_QuarterTurn_RotatesTheCornersAroundTheCentre()
    {
        var corners = new Vector2[4];

        SpriteQuad.Corners(new Vector2(0f, 0f), new Vector2(4f, 2f), MathF.PI / 2f, corners);

        // The centre is (2, 1) and the offsets map (x, y) to (-y, x) at a quarter turn.
        corners[0].X.Should().BeApproximately(3f, 1e-5f);
        corners[0].Y.Should().BeApproximately(-1f, 1e-5f);
        corners[1].X.Should().BeApproximately(3f, 1e-5f);
        corners[1].Y.Should().BeApproximately(3f, 1e-5f);
        corners[2].X.Should().BeApproximately(1f, 1e-5f);
        corners[2].Y.Should().BeApproximately(-1f, 1e-5f);
        corners[3].X.Should().BeApproximately(1f, 1e-5f);
        corners[3].Y.Should().BeApproximately(3f, 1e-5f);
    }

    [Fact]
    public void SpriteQuad_HalfTurn_KeepsTheCentreAndSwapsTheCorners()
    {
        var corners = new Vector2[4];

        SpriteQuad.Corners(new Vector2(0f, 0f), new Vector2(4f, 2f), MathF.PI, corners);

        corners[0].X.Should().BeApproximately(4f, 1e-5f);
        corners[0].Y.Should().BeApproximately(2f, 1e-5f);
        corners[3].X.Should().BeApproximately(0f, 1e-5f);
        corners[3].Y.Should().BeApproximately(0f, 1e-5f);
    }

    [Fact]
    public void SpriteQuad_FullTurn_ReturnsTheRectangleAgain()
    {
        var corners = new Vector2[4];

        SpriteQuad.Corners(new Vector2(5f, 5f), new Vector2(2f, 6f), MathF.Tau, corners);

        corners[0].X.Should().BeApproximately(5f, 1e-5f);
        corners[0].Y.Should().BeApproximately(5f, 1e-5f);
        corners[3].X.Should().BeApproximately(7f, 1e-5f);
        corners[3].Y.Should().BeApproximately(11f, 1e-5f);
    }

    [Fact]
    public void SpriteQuad_Bounds_WithoutRotation_IsTheRectangle()
    {
        Aabb bounds = SpriteQuad.Bounds(new Vector2(10f, 20f), new Vector2(30f, 40f), 0f);

        bounds.Left.Should().Be(10f);
        bounds.Top.Should().Be(20f);
        bounds.Right.Should().Be(40f);
        bounds.Bottom.Should().Be(60f);
    }

    [Fact]
    public void SpriteQuad_Bounds_OfAQuarterTurn_CoversTheTurnedQuad()
    {
        Aabb bounds = SpriteQuad.Bounds(new Vector2(0f, 0f), new Vector2(4f, 2f), MathF.PI / 2f);

        // The centre is (2, 1), so the turned quad spans two pixels to either side of it.
        bounds.Left.Should().BeApproximately(1f, 1e-5f);
        bounds.Top.Should().BeApproximately(-1f, 1e-5f);
        bounds.Right.Should().BeApproximately(3f, 1e-5f);
        bounds.Bottom.Should().BeApproximately(3f, 1e-5f);
    }

    [Fact]
    public void SpriteQuad_Bounds_OfANegativeSize_IsNormalized()
    {
        Aabb bounds = SpriteQuad.Bounds(new Vector2(10f, 20f), new Vector2(-30f, -40f), 0f);

        bounds.Left.Should().Be(-20f);
        bounds.Top.Should().Be(-20f);
        bounds.Right.Should().Be(10f);
        bounds.Bottom.Should().Be(20f);
    }

    [Fact]
    public void SpriteQuad_TooFewCorners_Throws() =>
        FluentActions.Invoking(() => SpriteQuad.Corners(Vector2.Zero, new Vector2(1f, 1f), 0f, new Vector2[3]))
            .Should().Throw<ArgumentOutOfRangeException>();
}
