using Age.Core;
using Age.Input;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

/// <summary>
/// Pins the console page of the developer window to what a person reads in it: a line that is wider than the page is broken into rows
/// rather than running off the side of the window, the newest rows are the ones that fit, and a click on any row of a wrapped line
/// copies the whole line.
/// </summary>
public sealed class ConsoleTabTests
{
    [Fact]
    public void ConsoleTab_ALineWiderThanThePage_IsDrawnAsSeveralRowsRatherThanRunningOff()
    {
        var console = new ConsoleService();
        console.Write(new string('a', 30));
        var tab = new ConsoleTab(console);
        var renderer = new RecordingRenderer();

        // The built-in font is 8 pixels wide, so a page of 80 pixels holds ten characters on a row: thirty of them are three rows.
        tab.Render(renderer, new Rect(Vector2.Zero, new Vector2(80f, 400f)));

        renderer.Texts.Should().HaveCount(3, "a line of thirty characters is broken into rows of ten");
        renderer.Texts.Should().OnlyContain(text => text.Length <= 10, "no row is wider than the page");
        renderer.Texts[0].Should().Be(new string('a', 10));
    }

    [Fact]
    public void ConsoleTab_ALineThatBreaksBetweenWords_BreaksAtASpaceWhenOneIsFarEnoughIn()
    {
        var console = new ConsoleService();
        console.Write("hello world again and more");
        var tab = new ConsoleTab(console);
        var renderer = new RecordingRenderer();

        tab.Render(renderer, new Rect(Vector2.Zero, new Vector2(80f, 400f)));

        // A row of ten characters that ends well inside a word is broken at the space it holds instead, so "hello" is a row of its
        // own rather than "hello worl" running a word onto the row after it.
        renderer.Texts.Should().Contain("hello");
        renderer.Texts.Should().NotContain("hello worl", "a row breaks between two words when the space is far enough in to be worth it");
    }

    [Fact]
    public void ConsoleTab_APageShorterThanTheOutput_ShowsTheNewestRows()
    {
        var console = new ConsoleService();
        console.Write("old");

        for (int index = 0; index < 20; index++)
        {
            console.Write($"new {index}");
        }

        var tab = new ConsoleTab(console);
        var renderer = new RecordingRenderer();

        // The rows are twelve pixels apart, so a page of forty pixels holds three of them: the three newest lines are read.
        tab.Render(renderer, new Rect(Vector2.Zero, new Vector2(800f, 40f)));

        renderer.Texts.Should().HaveCount(3);
        renderer.Texts.Should().NotContain("old", "a page that is too short shows the end of the output rather than its beginning");
        renderer.Texts[^1].Should().Be("new 19", "the newest line is the last one drawn");
    }

    [Fact]
    public void ConsoleTab_AClickOnARowOfAWrappedLine_CopiesTheWholeLine()
    {
        var console = new ConsoleService();
        console.Write(new string('a', 30));
        var clipboard = new RecordingClipboard();
        var tab = new ConsoleTab(console, clipboard);
        var renderer = new RecordingRenderer();
        var body = new Rect(Vector2.Zero, new Vector2(80f, 400f));

        tab.Render(renderer, body);

        // The rows are drawn from the top, so the second row of the only line sits one row below the first: a click there copies
        // the line whole rather than the ten characters that happen to be on that row.
        tab.Update(new GameTime(0.016d, 0.016d), body, new WindowPointer(new Vector2(4f, BitmapFontMetrics.GlyphHeight + 4f), true, true));

        clipboard.Text.Should().Be(new string('a', 30), "a row of a wrapped line copies the line it came from");
    }

    /// <summary>A renderer that keeps the text that was drawn on it, which is how a test reads the rows a page drew.</summary>
    private sealed class RecordingRenderer : IRenderer
    {
        public List<string> Texts { get; } = [];

        public Vector2 ViewportSize => new(800f, 600f);

        public void Attach(IWindowService window) => throw new NotSupportedException();

        public void SetCamera(Camera2D camera)
        {
        }

        public void BeginFrame(bool clear)
        {
        }

        public void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color, float rotation = 0f) => throw new NotSupportedException();

        public void DrawTextureRegion(TextureHandle texture, Rect source, Vector2 position, Vector2 size, Color color, float rotation = 0f) => throw new NotSupportedException();

        public TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height) => throw new NotSupportedException();

        public void ReleaseTexture(TextureHandle texture) => throw new NotSupportedException();

        public void DrawRectangle(Rect rect, Color color)
        {
        }

        public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color) => Texts.Add(text.ToString());

        public void EndFrame()
        {
        }

        public void Dispose()
        {
        }
    }

    /// <summary>A clipboard that remembers what was copied into it, which is how a test reads what a click did.</summary>
    private sealed class RecordingClipboard : IClipboardService
    {
        public string Text { get; private set; } = string.Empty;

        public void SetText(string? text) => Text = text ?? string.Empty;
    }
}
