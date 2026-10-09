using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class TextLayouterTests
{
    [Fact]
    public void TextLayouter_TextWithoutABox_KeepsTheLinesItWasWrittenWith()
    {
        TextLayout layout = TextLayouter.Layout("one\r\ntwo\nthree", new Monospace(), new TextStyle(), Vector2.Zero);

        layout.Lines.Select(line => line.Text).Should().Equal("one", "two", "three");
        layout.Size.Should().Be(new Vector2(30f, 30f), "the widest line is three characters of six pixels and there are three lines of ten");
        layout.Lines[2].Position.Should().Be(new Vector2(0f, 20f));
    }

    [Fact]
    public void TextLayouter_LineWiderThanTheBox_BreaksAtASpace()
    {
        TextLayout layout = TextLayouter.Layout("aaa bbb ccc", new Monospace(), new TextStyle(), new Vector2(48f, 0f));

        layout.Lines.Select(line => line.Text).Should().Equal("aaa bbb", "ccc");
        layout.Size.Should().Be(new Vector2(42f, 20f));
    }

    [Fact]
    public void TextLayouter_WordWiderThanTheBox_OverflowsItUnlessTheStyleBreaksWords()
    {
        TextLayout kept = TextLayouter.Layout("abcdefghij", new Monospace(), new TextStyle(), new Vector2(36f, 0f));
        TextLayout broken = TextLayouter.Layout("abcdefghij", new Monospace(), new TextStyle { Wrap = TextWrap.Anywhere }, new Vector2(36f, 0f));

        kept.Lines.Select(line => line.Text).Should().Equal("abcdefghij");
        kept.Size.X.Should().Be(60f, "a word that does not fit is left to overflow when the style does not break words");
        broken.Lines.Select(line => line.Text).Should().Equal("abcdef", "ghij");
    }

    [Fact]
    public void TextLayouter_WrapNoneAndABoxWithoutWidth_BreakNothing()
    {
        TextLayout none = TextLayouter.Layout("aaa bbb ccc", new Monospace(), new TextStyle { Wrap = TextWrap.None }, new Vector2(36f, 0f));
        TextLayout noWidth = TextLayouter.Layout("aaa bbb ccc", new Monospace(), new TextStyle(), Vector2.Zero);

        none.Lines.Select(line => line.Text).Should().Equal("aaa bbb ccc");
        noWidth.Lines.Select(line => line.Text).Should().Equal("aaa bbb ccc");
    }

    [Fact]
    public void TextLayouter_Alignment_PlacesALineInTheBox()
    {
        var style = new TextStyle { Align = TextAlign.Center };
        TextLayout centered = TextLayouter.Layout("one", new Monospace(), style, new Vector2(60f, 0f));
        TextLayout end = TextLayouter.Layout("one", new Monospace(), new TextStyle { Align = TextAlign.End }, new Vector2(60f, 0f));

        centered.Lines[0].Position.X.Should().Be(21f, "a line of eighteen pixels is centered in sixty");
        end.Lines[0].Position.X.Should().Be(42f);
    }

    [Fact]
    public void TextLayouter_VerticalAlignment_PlacesTheBlockOfLinesInTheBox()
    {
        var style = new TextStyle { VerticalAlign = TextVerticalAlign.Middle };
        TextLayout middle = TextLayouter.Layout("one\ntwo", new Monospace(), style, new Vector2(0f, 100f));
        TextLayout bottom = TextLayouter.Layout("one\ntwo", new Monospace(), new TextStyle { VerticalAlign = TextVerticalAlign.Bottom }, new Vector2(0f, 100f));

        middle.Lines[0].Position.Y.Should().Be(40f, "a block of twenty pixels is centered in a box of one hundred");
        bottom.Lines[0].Position.Y.Should().Be(80f);
    }

    [Fact]
    public void TextLayouter_LinesThatDoNotFitTheHeight_AreDroppedOrMarkedWithAnEllipsis()
    {
        TextLayout clip = TextLayouter.Layout("one\ntwo\nthree", new Monospace(), new TextStyle { Overflow = TextOverflow.Clip }, new Vector2(0f, 20f));
        TextLayout marked = TextLayouter.Layout("one\ntwo\nthree", new Monospace(), new TextStyle { Overflow = TextOverflow.Ellipsis }, new Vector2(0f, 20f));

        clip.Lines.Select(line => line.Text).Should().Equal("one", "two");
        marked.Lines.Select(line => line.Text).Should().Equal("one", "two...");
        marked.Lines[1].Size.X.Should().Be(36f, "the marked line is as long as its characters");
    }

    [Fact]
    public void TextLayouter_ShortenedLine_KeepsWhatFitsTheWidth()
    {
        var style = new TextStyle { Overflow = TextOverflow.Ellipsis };
        TextLayout layout = TextLayouter.Layout("aaa bbb ccc\nddd", new Monospace(), style, new Vector2(48f, 10f));

        layout.Lines.Should().ContainSingle();
        layout.Lines[0].Text.Should().Be("aaa b...", "the longest prefix that fits the box and the ellipsis is kept");
        layout.Lines[0].Size.X.Should().BeLessThanOrEqualTo(48f);
    }

    [Fact]
    public void TextLayouter_LineSpacing_AddsRoomBetweenTheLines()
    {
        TextLayout layout = TextLayouter.Layout("one\ntwo", new Monospace(), new TextStyle { LineSpacing = 4f }, Vector2.Zero);

        layout.Lines[1].Position.Y.Should().Be(14f);
        layout.Size.Y.Should().Be(24f, "two lines of ten pixels and the four between them");
    }

    [Fact]
    public void TextLayouter_BoxShorterThanALine_HoldsNoLine()
    {
        TextLayout layout = TextLayouter.Layout("one", new Monospace(), new TextStyle { Overflow = TextOverflow.Clip }, new Vector2(30f, 5f));

        layout.Lines.Should().BeEmpty();
        layout.Size.Should().Be(Vector2.Zero);
    }

    [Fact]
    public void TextLayouter_EmptyText_HasNoLineAtAll()
    {
        TextLayouter.Layout(null, new Monospace(), new TextStyle(), Vector2.Zero).Should().BeSameAs(TextLayout.Empty);
        TextLayouter.Layout(string.Empty, new Monospace(), new TextStyle(), Vector2.Zero).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void TextLayouter_EveryLine_IsAsWideAsItsCharacters()
    {
        TextLayout layout = TextLayouter.Layout("one\ntwooo", new Monospace(), new TextStyle(), Vector2.Zero);

        layout.Lines[0].Size.Should().Be(new Vector2(18f, 10f));
        layout.Lines[1].Size.Should().Be(new Vector2(30f, 10f));
        layout.Size.X.Should().Be(30f, "the size of a text is the width of its widest line");
    }

    [Fact]
    public void TextLayouter_TextThatMayOverflowTheBox_KeepsEveryLine()
    {
        TextLayout visible = TextLayouter.Layout("one\ntwo\nthree", new Monospace(), new TextStyle(), new Vector2(0f, 20f));
        TextLayout clipped = TextLayouter.Layout("one\ntwo\nthree", new Monospace(), new TextStyle { Overflow = TextOverflow.Clip }, new Vector2(0f, 20f));

        visible.Lines.Select(line => line.Text).Should().Equal("one", "two", "three");
        visible.Size.Y.Should().Be(30f);
        clipped.Lines.Select(line => line.Text).Should().Equal("one", "two");
    }

    private sealed class Monospace : ITextMeasurer
    {
        public FontMetrics Metrics => new(8f, 10f);

        public Vector2 Measure(ReadOnlySpan<char> text) => new(text.Length * 6f, 10f);

        public FontHandle? Font(char value) => null;
    }
}
