using Age.Core;
using Age.UI;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

/// <summary>
/// Pins what the anchors of an element do to the rectangle of it, which is the arithmetic that a resolution of any size and
/// shape rests on. The values here are the ones of a canvas of 1280 by 720, which is the resolution the sample is authored
/// against.
/// </summary>
public sealed class RectTransformTests
{
    private const float Tolerance = 1e-4f;

    private static readonly Rect Canvas = new(Vector2.Zero, new Vector2(1280f, 720f));

    [Fact]
    public void RectTransform_Resolve_APointAnchor_PutsTheAnchoredPositionWhereTheCanvasSays()
    {
        var rect = new RectTransformComponent
        {
            Anchored = true,
            AnchoredPosition = new Vector2(100f, 100f),
            SizeDelta = new Vector2(200f, 50f),
        };

        rect.Resolve(Canvas, 1f);

        rect.Position.Should().Be(new Vector2(100f, 100f));
        rect.Size.Should().Be(new Vector2(200f, 50f));
    }

    [Fact]
    public void RectTransform_Resolve_ABottomRightAnchorWithAPivot_KeepsTheMarginFromThatCorner()
    {
        var rect = new RectTransformComponent
        {
            Anchored = true,
            AnchorMin = new Vector2(1f, 1f),
            AnchorMax = new Vector2(1f, 1f),
            Pivot = new Vector2(1f, 1f),
            AnchoredPosition = new Vector2(-100f, -100f),
            SizeDelta = new Vector2(200f, 50f),
        };

        rect.Resolve(Canvas, 1f);

        rect.Position.Should().Be(new Vector2(1280f - 300f, 720f - 150f));
        rect.Size.Should().Be(new Vector2(200f, 50f));
    }

    [Fact]
    public void RectTransform_Resolve_TwoOppositeAnchorsOfTheWholeCanvas_StretchToIt()
    {
        var rect = new RectTransformComponent
        {
            Anchored = true,
            AnchorMax = new Vector2(1f, 1f),
        };

        rect.Resolve(Canvas, 1f);

        rect.Position.Should().Be(Vector2.Zero);
        rect.Size.Should().Be(new Vector2(1280f, 720f));
    }

    [Fact]
    public void RectTransform_Resolve_StretchedAroundTheMiddleWithAMargin_KeepsTheMarginOnEverySide()
    {
        var rect = new RectTransformComponent
        {
            Anchored = true,
            AnchorMax = new Vector2(1f, 1f),
            Pivot = new Vector2(0.5f, 0.5f),
            SizeDelta = new Vector2(-16f, -16f),
        };

        rect.Resolve(Canvas, 1f);

        rect.Position.Should().Be(new Vector2(8f, 8f));
        rect.Size.Should().Be(new Vector2(1264f, 704f));
    }

    [Fact]
    public void RectTransform_Resolve_AnAnchorInsideTheCanvas_FollowsTheProportionOfIt()
    {
        // Half way across and a quarter of the way down, which is the middle of the top edge.
        var rect = new RectTransformComponent
        {
            Anchored = true,
            AnchorMin = new Vector2(0.5f, 0.25f),
            AnchorMax = new Vector2(0.5f, 0.25f),
            SizeDelta = new Vector2(40f, 40f),
        };

        rect.Resolve(Canvas, 1f);

        rect.Position.Should().Be(new Vector2(640f, 180f));
        rect.Size.Should().Be(new Vector2(40f, 40f));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void RectTransform_Resolve_TheScaleOfTheCanvas_ReachesTheAnchorsAndTheSizeOfThem(float scale)
    {
        var rect = new RectTransformComponent
        {
            Anchored = true,
            AnchoredPosition = new Vector2(100f, 100f),
            SizeDelta = new Vector2(200f, 50f),
        };

        rect.Resolve(Canvas, scale);

        rect.Position.Should().Be(new Vector2(100f, 100f) * scale);
        rect.Size.Should().Be(new Vector2(200f, 50f) * scale);
    }

    [Theory]
    [InlineData(1280f, 720f)]
    [InlineData(1920f, 1080f)]
    [InlineData(3840f, 2160f)]
    [InlineData(1440f, 900f)]
    public void RectTransform_Resolve_TheCornersOfTheCanvas_AreTheCornersOfTheWindowAtEveryResolution(float width, float height)
    {
        // What a resolution of another size or shape changes is the scale, and every corner element of the canvas has to sit on
        // the corner of the window whatever that scale is.
        var canvas = new CanvasComponent
        {
            IsRoot = true,
            DesignSize = new Vector2(1280f, 720f),
            ScaleMode = CanvasScaleMode.ScaleWithScreenSize,
            Match = 0.5f,
        };

        canvas.Resolve(new Vector2(width, height));

        var area = new Rect(Vector2.Zero, canvas.Resolution);
        var topLeft = new RectTransformComponent { Anchored = true, SizeDelta = new Vector2(64f, 64f) };
        var bottomRight = new RectTransformComponent
        {
            Anchored = true,
            AnchorMin = new Vector2(1f, 1f),
            AnchorMax = new Vector2(1f, 1f),
            Pivot = new Vector2(1f, 1f),
            SizeDelta = new Vector2(64f, 64f),
        };

        topLeft.Resolve(area, canvas.Scale);
        bottomRight.Resolve(area, canvas.Scale);

        topLeft.Position.Should().Be(Vector2.Zero);
        topLeft.Size.Should().Be(new Vector2(64f, 64f) * canvas.Scale);
        (bottomRight.Position + bottomRight.Size).X.Should().BeApproximately(width, Tolerance);
        (bottomRight.Position + bottomRight.Size).Y.Should().BeApproximately(height, Tolerance);
    }
}
