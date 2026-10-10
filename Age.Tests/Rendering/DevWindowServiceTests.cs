using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

/// <summary>
/// Pins the developer window that stands beside the game to what a person does with it: it opens a window of its own and keeps its
/// pages while it is closed, the cross of the window closes it, the cross of a tab removes that page, and the page that is shown is
/// the one that is drawn.
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

    /// <summary>A page that remembers whether it was read, which is how a test reads which page is on top.</summary>
    private sealed class FakeTab(string title) : IDevWindowTab
    {
        public string Title { get; } = title;

        public bool Updated { get; private set; }

        public void Update(in GameTime frame, Rect body) => Updated = true;

        public void Render(IRenderer renderer, Rect body)
        {
        }
    }
}
