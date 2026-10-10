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
}
