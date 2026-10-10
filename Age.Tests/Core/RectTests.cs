using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

/// <summary>
/// Pins the geometry of <see cref="Rect.Intersect"/>, which is what makes a clip that nests mean the same thing to every
/// caller: the clip in force is the intersection of a push with the clip below it.
/// </summary>
public sealed class RectTests
{
    [Fact]
    public void Rect_Intersect_AOverlappingRect_KeepsTheSharedPart()
    {
        var first = new Rect(new Vector2(0f, 0f), new Vector2(100f, 80f));
        var second = new Rect(new Vector2(40f, 30f), new Vector2(100f, 80f));

        Rect shared = first.Intersect(second);

        shared.Position.Should().Be(new Vector2(40f, 30f));
        shared.Size.Should().Be(new Vector2(60f, 50f));
    }

    [Fact]
    public void Rect_Intersect_ARectInsideAnother_IsTheInnerOne()
    {
        var outer = new Rect(new Vector2(0f, 0f), new Vector2(100f, 100f));
        var inner = new Rect(new Vector2(10f, 20f), new Vector2(30f, 40f));

        outer.Intersect(inner).Should().Be(inner);
    }

    [Fact]
    public void Rect_Intersect_IsCommutative()
    {
        var first = new Rect(new Vector2(5f, 5f), new Vector2(50f, 50f));
        var second = new Rect(new Vector2(30f, 10f), new Vector2(50f, 50f));

        first.Intersect(second).Should().Be(second.Intersect(first));
    }

    [Fact]
    public void Rect_Intersect_RectsThatDoNotTouch_CoverNothing()
    {
        var first = new Rect(new Vector2(0f, 0f), new Vector2(10f, 10f));
        var second = new Rect(new Vector2(20f, 20f), new Vector2(10f, 10f));

        Rect shared = first.Intersect(second);

        shared.Size.Should().Be(Vector2.Zero, "an intersection that covers nothing has a size of zero rather than a negative one");
    }

    [Fact]
    public void Rect_Intersect_AnEmptyResult_CanBeIntersectedAgain()
    {
        var first = new Rect(new Vector2(0f, 0f), new Vector2(10f, 10f));
        var second = new Rect(new Vector2(20f, 20f), new Vector2(10f, 10f));
        var third = new Rect(new Vector2(0f, 0f), new Vector2(100f, 100f));

        Rect shared = first.Intersect(second).Intersect(third);

        shared.Size.Should().Be(Vector2.Zero, "a chain of clips keeps working after a pair of them stop overlapping");
    }

    [Fact]
    public void Rect_Intersect_AClipCutToTheSurface_KeepsOnlyTheVisiblePart()
    {
        // What the renderer does to a clip before it points the scissor at it: a clip that begins outside the surface keeps
        // the part that is on the surface rather than a size taken from the corner that is off it.
        var surface = new Rect(Vector2.Zero, new Vector2(800f, 600f));
        var clip = new Rect(new Vector2(-50f, -20f), new Vector2(200f, 100f));

        Rect visible = clip.Intersect(surface);

        visible.Position.Should().Be(Vector2.Zero);
        visible.Size.Should().Be(new Vector2(150f, 80f));
    }

    [Fact]
    public void Rect_Intersect_AClipPastTheSurface_CoversNothing()
    {
        var surface = new Rect(Vector2.Zero, new Vector2(800f, 600f));
        var clip = new Rect(new Vector2(900f, 700f), new Vector2(100f, 100f));

        clip.Intersect(surface).Size.Should().Be(Vector2.Zero, "a clip that starts past the surface draws nothing");
    }
}
