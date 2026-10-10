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

    [Fact]
    public void DevWindowService_TheWindowManagerClosingTheWindow_ClosesItAndTheNextOpenMakesANewOne()
    {
        var host = new FakeHost();
        var service = new DevWindowService(host);
        service.Add(new FakeTab("one"));
        service.Open();
        service.IsOpen.Should().BeTrue();

        // The window manager closed the window, which is what the cross of its own title bar does: the host reports that it is no
        // longer open, and the window is disposed here rather than kept, so the next `devwindow` opens a window of its own.
        host.CloseFromTheWindowManager();
        service.Pump(new GameTime(0.016d, 0.016d));

        service.IsOpen.Should().BeFalse();
        host.Disposed.Should().BeTrue("the window that the window manager closed is let go");
        service.Tabs.Should().ContainSingle("closing the window keeps its pages");

        // A window that was closed by its own cross opens again rather than being found half-closed and left standing.
        service.Open();
        service.IsOpen.Should().BeTrue();
        host.Created.Should().Be(2, "the window that went is replaced by one of its own");
    }

    [Fact]
    public void DevWindowService_ThePageOfATab_IsHeldInsideTheBodyOfTheWindow()
    {
        var host = new FakeHost();
        var tab = new FakeTab("one");
        var service = new DevWindowService(host);
        service.Add(tab);
        service.Open();

        service.Pump(new GameTime(0.016d, 0.016d));

        // The page draws in the body rather than the whole window. The body begins below the title bar and the row of tabs and is
        // inset by the padding, so a page that draws past the room it was given is cut off at the frame of the window.
        tab.RenderedBody.Y.Should().BeGreaterThan(0f, "the page stands below the title bar and the tabs");

        // The clip is pushed around the page and popped after it, so the frame that follows draws unclipped.
        host.Clip.Should().BeNull("the clip of the page does not outlive the frame of the window");
        host.Pushed.Should().BeEquivalentTo([tab.RenderedBody], "the page is drawn inside the body of the window");
    }

    [Fact]
    public void DevWindowService_APageThatThrows_StillEndsTheFrameOfTheWindow()
    {
        // A page that fails does not leave the frame of the window open: the host is ended in a finally, so the frame that follows
        // swaps a buffer that was ended rather than one that was begun and left.
        var host = new FakeHost();
        var service = new DevWindowService(host);
        service.Add(new FakeTab("one") { ThrowOnUpdate = true });
        service.Open();

        Action pump = () => service.Pump(new GameTime(0.016d, 0.016d));

        pump.Should().Throw<InvalidOperationException>();
        host.Began.Should().Be(1);
        host.Ended.Should().Be(1, "the frame of the window is ended even when a page throws");
    }

    [Fact]
    public void DevWindowService_AWindowClosedByTheWindowManager_EndsTheFrameExactlyOnce()
    {
        // The early return that a closed window takes closes the frame it began, and only once: the close does not end the frame a
        // second time.
        var host = new FakeHost();
        var service = new DevWindowService(host);
        service.Add(new FakeTab("one"));
        service.Open();

        host.CloseFromTheWindowManager();
        service.Pump(new GameTime(0.016d, 0.016d));

        host.Began.Should().Be(0, "a window that is already closed never begins a frame");
        host.Ended.Should().Be(0);

        // The next pump after the window was disposed opens a window of its own and pumps one frame, begun and ended once.
        service.Open();
        service.Pump(new GameTime(0.016d, 0.032d));

        host.Began.Should().Be(1);
        host.Ended.Should().Be(1);
    }

    /// <summary>A page that remembers whether it was read, which is how a test reads which page is on top.</summary>
    private sealed class FakeTab(string title) : IDevWindowTab
    {
        public string Title { get; } = title;

        public bool Updated { get; private set; }

        public bool Closable { get; init; }

        /// <summary>Gets the body the page was drawn in, which is what says the page was given the room inside the frame of the window.</summary>
        public Rect RenderedBody { get; private set; }

        /// <summary>Gets or sets a value indicating whether the page throws when it is read, which is what a page that fails looks like.</summary>
        public bool ThrowOnUpdate { get; init; }

        public void Update(in GameTime frame, Rect body, in WindowPointer pointer)
        {
            Updated = true;

            if (ThrowOnUpdate)
            {
                throw new InvalidOperationException("The page of the tab failed.");
            }
        }

        public void Render(IRenderer renderer, Rect body) => RenderedBody = body;
    }

    [Fact]
    public void DevWindowService_Focused_IsTrueOnlyWhileTheWindowIsOpenAndHasTheKeyboard()
    {
        var host = new NullDevWindowHost();
        var service = new DevWindowService(host);

        service.Focused.Should().BeFalse("a window that is not open has no keyboard to hold");

        service.Open();
        service.Focused.Should().BeFalse("the host of a run with no window is never given the focus");

        var focused = new FakeHost { Focused = true };
        var other = new DevWindowService(focused);
        other.Open();

        other.Focused.Should().BeTrue("the window of the operating system has the focus");

        // A window that the window manager closed holds no focus, whatever the host last reported, because the service is the one
        // that says whether the window is there at all.
        other.Close();
        other.Focused.Should().BeFalse();
    }

    /// <summary>A host that reports the size and a click a test sets, which is how the frame of the window is driven without a window.</summary>
    private sealed class FakeHost : IDevWindowHost
    {
        public Vector2 Click { get; set; }

        public bool IsOpen { get; private set; }

        /// <summary>Gets or sets whether the window has the focus, which a test moves the way the window manager does.</summary>
        public bool Focused { get; set; }

        public int Created { get; private set; }

        public bool Disposed { get; private set; }

        public Vector2 Size { get; private set; }

        public Vector2 Pointer { get; private set; }

        public bool PointerDown { get; private set; }

        /// <summary>Closes the window the way the window manager does, which is what the cross of the title bar of the window is.</summary>
        public void CloseFromTheWindowManager() => IsOpen = false;

        public void Create(int width, int height, string title)
        {
            Size = new Vector2(width, height);
            IsOpen = true;
            Created++;
        }

        public bool Pump(out Vector2? clicked)
        {
            clicked = Click;
            Pointer = Click;
            Click = default;
            return IsOpen;
        }

        public void BeginFrame(bool clear) => Began++;

        public void EndFrame() => Ended++;

        /// <summary>Gets the number of frames that were begun, which is what a test compares with the number that were ended.</summary>
        public int Began { get; private set; }

        /// <summary>Gets the number of frames that were ended, which is what says a frame was closed exactly once.</summary>
        public int Ended { get; private set; }

        public void DrawRectangle(Rect rect, Color color)
        {
        }

        public void DrawText(string text, Vector2 position, Color color)
        {
        }

        public Vector2 Measure(string text) => new(text.Length * BitmapFontMetrics.GlyphWidth, BitmapFontMetrics.GlyphHeight);

        /// <summary>Gets the clip that was pushed and not popped yet, which is what says a page was held inside the body.</summary>
        public Rect? Clip { get; private set; }

        /// <summary>Gets every clip that was pushed, in the order it was pushed, which is what says the page was clipped at all.</summary>
        public List<Rect> Pushed { get; } = [];

        public void PushClip(Rect rect)
        {
            Clip = rect;
            Pushed.Add(rect);
        }

        public void PopClip() => Clip = null;

        public void Dispose()
        {
            IsOpen = false;
            Disposed = true;
        }
    }
}
