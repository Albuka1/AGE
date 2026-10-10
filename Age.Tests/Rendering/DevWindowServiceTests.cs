using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

/// <summary>
/// Pins the developer window that stands beside the game to what a person does with it: it opens a window of its own and keeps its
/// pages while it is closed, the cross of a tab removes that page while a page that is not closable has none, and the page that is
/// shown is the one that is read.
/// </summary>
public sealed class DevWindowServiceTests
{
    [Fact]
    public void DevWindowService_Open_CreatesTheWindowOnceAndKeepsThePagesWhenItCloses()
    {
        var host = new NullDevWindowHost();
        var service = new DevWindowService(host);
        service.Add(new FakeTab("one"));

        service.IsOpen.Should().BeFalse("the window of the operating system is not there until it is asked for");
        host.IsOpen.Should().BeFalse();

        service.Open();
        service.IsOpen.Should().BeTrue();
        host.IsOpen.Should().BeTrue();

        service.Close();
        service.IsOpen.Should().BeFalse();
        service.Tabs.Should().HaveCount(1, "closing the window keeps its pages");
    }

    [Fact]
    public void DevWindowService_Pump_DrawsAndReadsOnlyThePageThatIsShown()
    {
        var host = new NullDevWindowHost();
        var first = new FakeTab("one");
        var second = new FakeTab("two");
        var service = new DevWindowService(host);
        service.Add(first).Add(second);
        service.Open();

        service.Pump(new GameTime(0.016d, 0.016d));

        first.Updated.Should().BeTrue("the page that is shown reads the frame");
        second.Updated.Should().BeFalse("a page that is not shown reads nothing");

        service.ActiveTab = 1;
        service.Pump(new GameTime(0.016d, 0.032d));

        second.Updated.Should().BeTrue();
    }

    [Fact]
    public void DevWindowService_ARemovedPage_IsGoneAndTheOneOnTopStaysOnTop()
    {
        var host = new NullDevWindowHost();
        var first = new FakeTab("one");
        var second = new FakeTab("two");
        var service = new DevWindowService(host);
        service.Add(first).Add(second);
        service.ActiveTab = 1;

        // The page before the one that is shown went, so the page that was on top is still on top.
        service.Remove(first).Should().BeTrue();
        service.Tabs.Should().HaveCount(1);
        service.ActiveTab.Should().Be(0);
        service.Tabs[0].Should().BeSameAs(second);

        service.Remove(second).Should().BeTrue();
        service.Tabs.Should().BeEmpty();
        service.Remove(second).Should().BeFalse("a page that is not there is nothing to remove");
    }

    [Fact]
    public void DevWindowService_ASize_IsRefusedWhenASideIsNotPositive()
    {
        var service = new DevWindowService(new NullDevWindowHost());

        Action zero = () => service.Size = new Vector2(0f, 100f);
        Action negative = () => service.Size = new Vector2(100f, -1f);

        zero.Should().Throw<ArgumentOutOfRangeException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DevWindowService_TheCrossOfAClosablePageRemovesItAndANonClosablePageHasNone()
    {
        var host = new FakeHost { Click = new Vector2(82f, 39f) };
        var closable = new FakeTab("one") { Closable = true };
        var fixedTab = new FakeTab("two") { Closable = false };
        var service = new DevWindowService(host);
        service.Add(closable).Add(fixedTab);
        service.Open();

        // The cross of the first page sits at the right end of its label, which is 96 pixels wide at least: a click there removes it.
        service.Pump(new GameTime(0.016d, 0.016d));
        service.Tabs.Should().ContainSingle().Which.Should().BeSameAs(fixedTab);

        // The second page reports what the engine holds and has no cross, so the same click neither removes it nor closes the window.
        host.Click = new Vector2(96f + 82f, 39f);
        service.Pump(new GameTime(0.016d, 0.032d));

        service.IsOpen.Should().BeTrue("the window has no cross of its own");
        service.Tabs.Should().ContainSingle("a page that is not closable has no cross to press");
    }

    /// <summary>A page that remembers whether it was read, which is how a test reads which page is on top.</summary>
    private sealed class FakeTab(string title) : IDevWindowTab
    {
        public string Title { get; } = title;

        public bool Updated { get; private set; }

        public bool Closable { get; init; }

        public void Update(in GameTime frame, Rect body, in WindowPointer pointer) => Updated = true;

        public void Render(IRenderer renderer, Rect body)
        {
        }
    }

    /// <summary>A host that reports the size and a click a test sets, which is how the frame of the window is driven without a window.</summary>
    private sealed class FakeHost : IDevWindowHost
    {
        public Vector2 Click { get; set; }

        public bool IsOpen { get; private set; }

        public Vector2 Size { get; private set; }

        public Vector2 Pointer { get; private set; }

        public bool PointerDown { get; private set; }

        public void Create(int width, int height, string title)
        {
            Size = new Vector2(width, height);
            IsOpen = true;
        }

        public bool Pump(out Vector2? clicked)
        {
            clicked = Click;
            Pointer = Click;
            Click = default;
            return IsOpen;
        }

        public void BeginFrame(bool clear)
        {
        }

        public void EndFrame()
        {
        }

        public void DrawRectangle(Rect rect, Color color)
        {
        }

        public void DrawText(string text, Vector2 position, Color color)
        {
        }

        public Vector2 Measure(string text) => new(text.Length * BitmapFontMetrics.GlyphWidth, BitmapFontMetrics.GlyphHeight);

        public void Dispose() => IsOpen = false;
    }
}
