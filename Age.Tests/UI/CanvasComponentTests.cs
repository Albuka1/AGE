using Age.Core;
using Age.UI;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

/// <summary>
/// Pins the scaler of a canvas: the scale that turns a design unit into a pixel of the window, and the size of the canvas in
/// design units that follows from it. This is what makes one layout fit displays of different resolutions.
/// </summary>
public sealed class CanvasComponentTests
{
    private const float Tolerance = 1e-4f;

    private static readonly Vector2 Design = new(1280f, 720f);

    [Fact]
    public void CanvasComponent_Resolve_WithoutAScaler_TheCanvasIsTheWindow()
    {
        var canvas = new CanvasComponent { DesignSize = Design };

        canvas.Resolve(new Vector2(1920f, 1080f));

        canvas.Scale.Should().Be(1f);
        canvas.Resolution.Should().Be(new Vector2(1920f, 1080f));
    }

    [Fact]
    public void CanvasComponent_Resolve_AScalerWithoutADesignSize_FallsBackToPixels()
    {
        var canvas = new CanvasComponent { ScaleMode = CanvasScaleMode.ScaleWithScreenSize };

        canvas.Resolve(new Vector2(1920f, 1080f));

        canvas.Scale.Should().Be(1f);
        canvas.Resolution.Should().Be(new Vector2(1920f, 1080f));
    }

    [Theory]
    [InlineData(1280f, 720f)]
    [InlineData(1920f, 1080f)]
    [InlineData(3840f, 2160f)]
    public void CanvasComponent_Resolve_AWindowOfTheShapeOfTheDesign_ShowsTheWholeOfIt(float width, float height)
    {
        var canvas = new CanvasComponent { ScaleMode = CanvasScaleMode.ScaleWithScreenSize, DesignSize = Design };

        canvas.Resolve(new Vector2(width, height));

        canvas.Scale.Should().BeApproximately(width / Design.X, Tolerance);
        canvas.Resolution.X.Should().BeApproximately(Design.X, 0.01f);
        canvas.Resolution.Y.Should().BeApproximately(Design.Y, 0.01f);
    }

    [Fact]
    public void CanvasComponent_Resolve_MatchingTheWidth_KeepsTheDesignAcrossTheScreen()
    {
        var canvas = new CanvasComponent { ScaleMode = CanvasScaleMode.ScaleWithScreenSize, DesignSize = Design, Match = 0f };

        canvas.Resolve(new Vector2(1920f, 1200f));

        canvas.Scale.Should().BeApproximately(1.5f, Tolerance);
        canvas.Resolution.X.Should().BeApproximately(1280f, Tolerance);
        canvas.Resolution.Y.Should().BeApproximately(800f, Tolerance);
    }

    [Fact]
    public void CanvasComponent_Resolve_MatchingTheHeight_KeepsTheDesignDownTheScreen()
    {
        var canvas = new CanvasComponent { ScaleMode = CanvasScaleMode.ScaleWithScreenSize, DesignSize = Design, Match = 1f };

        canvas.Resolve(new Vector2(1920f, 1200f));

        canvas.Scale.Should().BeApproximately(1200f / 720f, Tolerance);
        canvas.Resolution.X.Should().BeApproximately(1152f, 0.01f);
        canvas.Resolution.Y.Should().BeApproximately(720f, 0.01f);
    }

    [Fact]
    public void CanvasComponent_Resolve_AValueBetweenTheAxes_BlendsTheTwoRatios()
    {
        var canvas = new CanvasComponent { ScaleMode = CanvasScaleMode.ScaleWithScreenSize, DesignSize = Design, Match = 0.5f };

        canvas.Resolve(new Vector2(1920f, 1200f));

        // The geometric mean of the two ratios, which is the scale that is as much over one axis as it is under the other.
        canvas.Scale.Should().BeApproximately(MathF.Sqrt(1.5f * (1200f / 720f)), Tolerance);
        canvas.Scale.Should().BeGreaterThan(1.5f).And.BeLessThan(1200f / 720f);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(2f)]
    public void CanvasComponent_Resolve_AMatchOutsideTheAxes_IsClamped(float match)
    {
        var canvas = new CanvasComponent { ScaleMode = CanvasScaleMode.ScaleWithScreenSize, DesignSize = Design, Match = match };

        canvas.Resolve(new Vector2(1920f, 1200f));

        float expected = match < 0f ? 1.5f : 1200f / 720f;

        canvas.Scale.Should().BeApproximately(expected, Tolerance);
    }

    [Fact]
    public void CanvasComponent_Resolve_AWindowThatIsNotOnScreenYet_LaysNothingOut()
    {
        var canvas = new CanvasComponent { ScaleMode = CanvasScaleMode.ScaleWithScreenSize, DesignSize = Design };

        canvas.Resolve(Vector2.Zero);

        // A window of nothing is treated as a single pixel rather than as nothing at all, so no scale of a canvas is a division
        // by zero and the layout of a frame that runs before the window is on screen is one frame of a wrong scale and no more.
        canvas.Scale.Should().BeApproximately(1f / Design.X, Tolerance);
        canvas.Resolution.X.Should().BeApproximately(Design.X, 0.01f);
    }
}
