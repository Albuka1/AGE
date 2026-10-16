using Age.Core;
using Age.Rendering;
using Age.UI;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

/// <summary>
/// Pins the service that holds the windows of a game to what a game does with it: it opens one, reads the elements it built,
/// updates them every frame, and hands a press to what the window that built the button registered.
/// </summary>
public sealed class UiWindowServiceTests
{
    [Fact]
    public void UiWindowService_Open_BuildsTheWindowAndCreatesACanvasWhenThereIsNone()
    {
        var world = new World();
        var window = new FakeWindow("one");
        var service = new UiWindowService([window]);
        Entity root = service.Open(world, "one");

        service.IsOpen("one").Should().BeTrue();
        window.Builds.Should().Be(1);
        world.IsAlive(root).Should().BeTrue();
        world.Has<PanelComponent>(root).Should().BeTrue("the root of a window is a panel");
        world.Enumerate<CanvasComponent>().Should().ContainSingle("the service creates a canvas when the world holds none");
    }

    [Fact]
    public void UiWindowService_Open_ReusesTheRootCanvasOfTheWorld()
    {
        var world = new World();
        Entity canvas = world.CreateEntity();
        world.Set(canvas, new CanvasComponent { IsRoot = true, DesignSize = new Vector2(1920f, 1080f), ScaleMode = CanvasScaleMode.ScaleWithScreenSize });
        var service = new UiWindowService([new FakeWindow("one")]);

        Entity root = service.Open(world, "one");

        // The root of the window stands inside the canvas the game made rather than beside it, so the scaler of the game is the one that reaches it.
        world.Resolve(world.Get<ParentComponent>(root).Parent).Should().Be(canvas);
        world.Enumerate<CanvasComponent>().Should().ContainSingle();
    }

    [Fact]
    public void UiWindowService_Open_IsIdempotentWhileTheWindowIsOpen()
    {
        var world = new World();
        var window = new FakeWindow("one");
        var service = new UiWindowService([window]);

        Entity first = service.Open(world, "one");
        Entity again = service.Open(world, "one");

        again.Should().Be(first);
        window.Builds.Should().Be(1, "a window that is already open is not built a second time");
    }

    [Fact]
    public void UiWindowService_Open_AWindowThatTheContainerDoesNotHold_IsRefused()
    {
        var service = new UiWindowService([]);
        var world = new World();

        Action open = () => service.Open(world, "nope");

        open.Should().Throw<InvalidOperationException>().WithMessage("*nope*");
    }

    [Fact]
    public void UiWindowService_UpdateFrame_ReadsEveryOpenWindow()
    {
        var world = new World();
        var first = new FakeWindow("one");
        var second = new FakeWindow("two");
        var service = new UiWindowService([first, second]);
        service.Open(world, "one");
        service.Open(world, "two");

        service.UpdateFrame(world, new GameTime(0.016d, 0.016d));

        first.Updates.Should().Be(1);
        second.Updates.Should().Be(1);
    }

    [Fact]
    public void UiWindowService_UpdateFrame_AWindowThatIsNotOpen_IsNotRead()
    {
        var world = new World();
        var window = new FakeWindow("one");
        var service = new UiWindowService([window]);

        service.UpdateFrame(world, new GameTime(0.016d, 0.016d));

        window.Updates.Should().Be(0);
    }

    [Fact]
    public void UiWindowService_Close_RemovesTheWindowAndHandsItsTreeToTheWorld()
    {
        var world = new World();
        var service = new UiWindowService([new FakeWindow("one")]);
        Entity root = service.Open(world, "one");

        service.Close(world, "one");
        service.IsOpen("one").Should().BeFalse();
        world.IsAlive(root).Should().BeFalse("the root of a closed window is on its way out");

        world.ApplyPending();

        world.IsAlive(root).Should().BeFalse();
        world.Has<RectTransformComponent>(root).Should().BeFalse("a closed window leaves nothing of itself behind");
    }

    [Fact]
    public void UiWindowService_Close_AWindowThatIsNotOpen_DoesNothing()
    {
        var world = new World();
        var service = new UiWindowService([new FakeWindow("one")]);

        Action close = () => service.Close(world, "one");

        close.Should().NotThrow();
    }

    [Fact]
    public void UiWindowService_Toggle_OpensAndClosesTheSameWindow()
    {
        var world = new World();
        var service = new UiWindowService([new FakeWindow("one")]);

        service.Toggle(world, "one");
        service.IsOpen("one").Should().BeTrue();

        service.Toggle(world, "one");
        service.IsOpen("one").Should().BeFalse();
    }

    [Fact]
    public void UiWindowService_CloseAll_ClosesEveryWindowThatIsOpen()
    {
        var world = new World();
        var service = new UiWindowService([new FakeWindow("one"), new FakeWindow("two")]);
        service.Open(world, "one");
        service.Open(world, "two");

        service.CloseAll(world);

        service.IsOpen("one").Should().BeFalse();
        service.IsOpen("two").Should().BeFalse();
    }

    [Fact]
    public void UiWindowService_Find_AnswersAnElementOfAnOpenWindow()
    {
        var world = new World();
        var service = new UiWindowService([new FakeWindow("one")]);
        service.Open(world, "one");

        Entity? title = service.Find(world, "one", "title");

        title.Should().NotBeNull();
        world.Get<UiElementComponent>(title!.Value).Id.Should().Be("title");
        service.Find(world, "one", "nope").Should().BeNull("a window holds no element that it did not build");
        service.Find(world, "nope", "title").Should().BeNull("a window that is not open holds nothing at all");
    }

    [Fact]
    public void UiWindowService_AButtonPress_CallsWhatTheWindowRegistered()
    {
        var world = new World();
        var window = new ClickWindow();
        var service = new UiWindowService([window]);
        service.Open(world, "click");

        world.Events.Raise(new ButtonPressedEvent(window.Button));
        world.Events.Dispatch();

        window.Presses.Should().Be(1, "the press of the frame reaches the window that built the button");
    }

    [Fact]
    public void UiWindowService_AButtonPressOfAClosedWindow_IsNotRead()
    {
        var world = new World();
        var window = new ClickWindow();
        var service = new UiWindowService([window]);
        service.Open(world, "click");
        Entity button = window.Button;

        service.Close(world, "click");
        world.Events.Raise(new ButtonPressedEvent(button));
        world.Events.Dispatch();

        window.Presses.Should().Be(0, "a window that is closed does not answer the press of the button it built");
    }

    [Fact]
    public void UiWindowService_AWindowThatThrowsWhenItIsBuilt_LeavesTheOpenMapAlone()
    {
        var world = new World();
        var service = new UiWindowService([new ThrowingWindow()]);

        Action open = () => service.Open(world, "boom");

        open.Should().Throw<InvalidOperationException>();
        service.IsOpen("boom").Should().BeFalse("a window that failed to build is not one the service holds as open");
    }

        /// <summary>A window that builds a label and remembers what its own build and update were asked for.</summary>
    private sealed class FakeWindow : IUiWindow
    {
        public FakeWindow(string id) => Id = id;

        public string Id { get; }

        public int Builds { get; private set; }

        public int Updates { get; private set; }

        public void Build(UiBuilder ui)
        {
            Builds++;
            ui.Label("title").Text(Id).At(16f, 16f).Size(200f, 24f);
        }

        public void Update(UiWindowContext context) => Updates++;
    }

    /// <summary>A window that builds a button and remembers how many times its handler was called.</summary>
    private sealed class ClickWindow : IUiWindow
    {
        public string Id => "click";

        public Entity Button { get; private set; }

        public int Presses { get; private set; }

        public void Build(UiBuilder ui)
        {
            ButtonHandle button = ui.Button("press").Text("Press").At(0f, 0f).Size(100f, 30f);
            Button = button.Entity;
            button.OnClick(() => Presses++);
        }

        public void Update(UiWindowContext context) { }
    }

    /// <summary>A window that throws when it is built, which is what a build of a window that cannot answer looks like.</summary>
    private sealed class ThrowingWindow : IUiWindow
    {
        public string Id => "boom";

        public void Build(UiBuilder ui) => throw new InvalidOperationException("The window of the boom cannot be built.");

        public void Update(UiWindowContext context) { }
    }
}