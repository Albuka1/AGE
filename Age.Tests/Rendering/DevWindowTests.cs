using Age.Core;
using Age.Input;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

/// <summary>
/// Pins the developer window to what a person does with it: it opens with its key, the title bar drags it and keeps it inside the
/// frame, a click on a tab shows that tab, the tab key walks them, and only the tab that is shown is drawn and read.
/// </summary>
public sealed class DevWindowTests
{
    [Fact]
    public void DevWindow_Update_OpensAndClosesWithItsKeyAndEscape()
    {
        var input = new FakeInput();
        var window = new DevWindow(input, new RecordingRenderer());

        window.IsOpen.Should().BeFalse();

        Press(window, input, Key.F1);
        window.IsOpen.Should().BeTrue();

        Press(window, input, Key.Escape);
        window.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void DevWindow_Update_TheTitleBarDragsTheWindowAndKeepsItInsideTheFrame()
    {
        var input = new FakeInput();
        var window = new DevWindow(input, new RecordingRenderer()) { Position = new Vector2(100f, 100f) };
        window.Open();

        // The window is grabbed in the middle of its title bar and follows the pointer by the offset it was grabbed at, so it does
        // not jump under the cursor the moment it is picked up.
        input.MovePointer(new Vector2(120f, 108f));
        input.PressMouse(MouseButton.Left);
        window.Update(new GameTime(0.016d, 0.016d));

        input.MovePointer(new Vector2(220f, 208f));
        input.BeginFrame();
        input.HoldMouse(MouseButton.Left);
        window.Update(new GameTime(0.016d, 0.032d));

        window.Position.Should().Be(new Vector2(200f, 200f));

        // A window that is dragged past an edge is kept inside the frame rather than lost off it.
        input.MovePointer(new Vector2(-1000f, -1000f));
        input.BeginFrame();
        input.HoldMouse(MouseButton.Left);
        window.Update(new GameTime(0.016d, 0.048d));

        window.Position.Should().Be(Vector2.Zero);
    }

    [Fact]
    public void DevWindow_Update_AClickOnATabShowsThatTabAndTheTabKeyWalksThem()
    {
        var input = new FakeInput();
        var window = new DevWindow(input, new RecordingRenderer());
        window.Add(new RecordingTab("one")).Add(new RecordingTab("two"));
        window.Open();

        window.ActiveTab.Should().Be(0, "the first tab that is added is the one that is shown");

        // The tab key walks the tabs forward, which is what a row of tabs is walked with at a keyboard.
        Press(window, input, Key.Tab);
        window.ActiveTab.Should().Be(1);

        Press(window, input, Key.Tab);
        window.ActiveTab.Should().Be(0, "walking past the last tab starts over at the first");

        // A left click on the second label shows the second tab, which is what a row of tabs is for with a pointer.
        input.MovePointer(new Vector2(150f, 76f));
        input.PressMouse(MouseButton.Left);
        window.Update(new GameTime(0.016d, 0.016d));

        window.ActiveTab.Should().Be(1);
    }

    [Fact]
    public void DevWindow_Render_DrawsAndReadsOnlyTheTabThatIsShown()
    {
        var input = new FakeInput();
        var first = new RecordingTab("one");
        var second = new RecordingTab("two");
        var renderer = new RecordingRenderer();
        var window = new DevWindow(input, renderer);
        window.Add(first).Add(second);
        window.Open();

        window.Update(new GameTime(0.016d, 0.016d));

        first.Updated.Should().BeTrue("the tab that is shown reads the frame");
        second.Updated.Should().BeFalse("a tab that is not shown reads nothing");

        window.Render(new World(), new Camera2D());

        first.Drawn.Should().BeTrue();
        second.Drawn.Should().BeFalse();

        // The page is drawn inside a clip of the body, which is what keeps a tab from drawing over the game around the window.
        renderer.Clips.Should().Be(1, "the body is clipped once");
        renderer.Unclips.Should().Be(1, "and the clip is undone once");
    }

    /// <summary>Presses a key for one frame and runs it, which is one frame of the loop.</summary>
    private static void Press(DevWindow window, FakeInput input, Key key, double delta = 0.016d)
    {
        input.BeginFrame();
        input.Press(key);
        window.Update(new GameTime(delta, 0d));
    }

    /// <summary>Clicks a point for one frame, which is how a cross or a tab is pressed with the pointer.</summary>
    private static void Click(DevWindow window, FakeInput input, Vector2 point, double delta = 0.016d)
    {
        input.MovePointer(point);
        input.BeginFrame();
        input.PressMouse(MouseButton.Left);
        window.Update(new GameTime(delta, 0d));
    }

    [Fact]
    public void DevWindow_Update_TheCrossOfATabRemovesItAndTheCrossOfTheTitleBarClosesTheWindow()
    {
        var input = new FakeInput();
        var first = new RecordingTab("one");
        var second = new RecordingTab("two");
        var window = new DevWindow(input, new RecordingRenderer()) { Position = new Vector2(64f, 64f) };
        window.Add(first).Add(second);
        window.Open();

        // The cross of the first tab sits at the right end of its label: the label is 96 pixels wide at least, and the cross is a
        // square of 12 pixels at its right end, just below the title bar.
        Click(window, input, new Vector2(64f + 96f - 26f + 13f, 64f + 26f + 13f));

        window.Tabs.Should().HaveCount(1, "the cross closes the tab it belongs to");
        window.Tabs[0].Should().BeSameAs(second);
        window.IsOpen.Should().BeTrue("closing a tab does not close the window");

        // The cross of the window stands at the right end of the title bar, which is what closes the whole window.
        Click(window, input, new Vector2(64f + 560f - 26f + 13f, 64f + 13f));

        window.IsOpen.Should().BeFalse();
    }

    /// <summary>A tab that remembers whether it was read and drawn, which is how a test reads which page is on top.</summary>
    private sealed class RecordingTab(string title) : IDevWindowTab
    {
        public string Title { get; } = title;

        public bool Updated { get; private set; }

        public bool Drawn { get; private set; }

        public void Update(in GameTime frame, Rect body) => Updated = true;

        public void Render(IRenderer renderer, Rect body) => Drawn = true;
    }

    /// <summary>Reports a pointer the test moves and the keys and buttons it presses or holds for one frame.</summary>
    private sealed class FakeInput : IInputService
    {
        private readonly HashSet<Key> _pressed = [];
        private readonly HashSet<Key> _down = [];
        private readonly HashSet<MouseButton> _mousePressed = [];
        private readonly HashSet<MouseButton> _mouseDown = [];
        private Vector2 _pointer;

        public void MovePointer(Vector2 position) => _pointer = position;

        public void Press(Key key)
        {
            _pressed.Add(key);
            _down.Add(key);
        }

        public void PressMouse(MouseButton button)
        {
            _mousePressed.Add(button);
            _mouseDown.Add(button);
        }

        public void HoldMouse(MouseButton button) => _mouseDown.Add(button);

        public void BeginFrame()
        {
            _pressed.Clear();
            _mousePressed.Clear();
        }

        public Vector2 MousePosition => _pointer;

        public float MouseWheel => 0f;

        public bool IsKeyDown(Key key) => _down.Contains(key);

        public bool IsKeyPressed(Key key) => _pressed.Contains(key);

        public bool IsMouseButtonDown(MouseButton button) => _mouseDown.Contains(button);

        public bool IsMouseButtonPressed(MouseButton button) => _mousePressed.Contains(button);
    }

    /// <summary>A renderer that counts the clips of the window, which is how a test reads that the body was clipped.</summary>
    private sealed class RecordingRenderer : IRenderer
    {
        public int Clips { get; private set; }

        public int Unclips { get; private set; }

        public Vector2 ViewportSize => new(1280f, 720f);

        public void Attach(IWindowService window) => throw new NotSupportedException();

        public void SetCamera(Camera2D camera) => throw new NotSupportedException();

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

        public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color)
        {
        }

        public void PushClip(Rect rect) => Clips++;

        public void PopClip() => Unclips++;

        public void EndFrame()
        {
        }

        public void Dispose()
        {
        }
    }
}
