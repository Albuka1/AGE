using Age.Core;
using Age.Input;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class DevOverlayTests
{
    [Fact]
    public void DevOverlay_Update_OpensTheConsoleWithItsKeyAndStandsTheClockStill()
    {
        var console = new ConsoleService();
        var input = new FakeInputService();
        var timestep = new FixedTimestep(FixedTimestep.DefaultStep);
        DevOverlay overlay = Create(console, input, timestep);

        Open(overlay, input);

        console.IsOpen.Should().BeTrue();
        timestep.Paused.Should().BeTrue("a console that is open is a game that waits");
    }

    [Fact]
    public void DevOverlay_Update_ClosingTheConsolePutsTheClockBackTheWayItWas()
    {
        var console = new ConsoleService();
        var input = new FakeInputService();
        var timestep = new FixedTimestep(FixedTimestep.DefaultStep) { Paused = true };
        DevOverlay overlay = Create(console, input, timestep);

        Open(overlay, input);
        Press(overlay, input, overlay.ConsoleKey);

        console.IsOpen.Should().BeFalse();
        timestep.Paused.Should().BeTrue("the game was already paused before the console opened");
    }

    [Fact]
    public void DevOverlay_Update_RunsTheLineThatWasTyped()
    {
        var console = new ConsoleService();
        var input = new FakeInputService();
        var text = new FakeTextInputService();
        DevOverlay overlay = Create(console, input, text: text);
        var ran = new List<string>();

        console.Register("spawn", "Puts sprites on screen.", arguments => ran.Add(string.Join(' ', arguments)));

        Open(overlay, input);

        text.Type("spawn 3");
        Frame(overlay, input);

        console.Input.Should().Be("spawn 3", "the characters of the frame reach the console while it is open");

        Press(overlay, input, Key.Enter);

        ran.Should().Equal("3");
        console.History.Should().Equal("spawn 3");
    }

    [Fact]
    public void DevOverlay_Update_BackspaceAndTheHistoryKeys_EditTheLine()
    {
        var console = new ConsoleService();
        var input = new FakeInputService();
        var text = new FakeTextInputService();
        DevOverlay overlay = Create(console, input, text: text);

        Open(overlay, input);

        text.Type("hello");
        Frame(overlay, input);
        console.Input.Should().Be("hello");

        Press(overlay, input, Key.Backspace);
        console.Input.Should().Be("hell", "the backspace key removes the last character");

        text.Type("o");
        Frame(overlay, input);
        console.Input.Should().Be("hello");

        Press(overlay, input, Key.Enter);
        console.History.Should().Equal("hello");

        Press(overlay, input, Key.Up);
        console.Input.Should().Be("hello", "the up key walks what was entered before");

        Press(overlay, input, Key.Down);
        console.Input.Should().BeEmpty("walking past the last line leaves an empty line");
    }

    [Fact]
    public void DevOverlay_Update_ItsSecondKeyHidesTheNumbers()
    {
        var console = new ConsoleService();
        var input = new FakeInputService();
        DevOverlay overlay = Create(console, input);

        overlay.ShowStats.Should().BeTrue();

        Press(overlay, input, overlay.StatsKey);

        overlay.ShowStats.Should().BeFalse();
    }

    [Fact]
    public void DevOverlay_Update_MeasuresTheFramesPerSecondOverAWindow()
    {
        var console = new ConsoleService();
        var input = new FakeInputService();
        DevOverlay overlay = Create(console, input);

        for (var frame = 0; frame < 6; frame++)
        {
            Frame(overlay, input, 0.1d);
        }

        overlay.FramesPerSecond.Should().BeApproximately(10d, 0.001d, "six frames of a tenth of a second are ten frames a second");
    }

    /// <summary>Opens the console of the overlay, which is the frame that a developer presses its key in.</summary>
    private static void Open(DevOverlay overlay, FakeInputService input) => Press(overlay, input, overlay.ConsoleKey);

    /// <summary>Presses a key for one frame and runs it, which is one frame of the loop: the frame opens, the device reports the key, the game reads it.</summary>
    private static void Press(DevOverlay overlay, FakeInputService input, Key key, double delta = 0.016d)
    {
        input.BeginFrame();
        input.Press(key);
        overlay.Update(new GameTime(delta, 0d));
    }

    /// <summary>Runs one frame: input opens its frame, which forgets the keys of the frame before, and the overlay reads it.</summary>
    private static void Frame(DevOverlay overlay, FakeInputService input, double delta = 0.016d)
    {
        input.BeginFrame();
        overlay.Update(new GameTime(delta, 0d));
    }

    /// <summary>Builds an overlay over the services a test drives, with the fakes it does not look at.</summary>
    private static DevOverlay Create(
        ConsoleService console,
        FakeInputService input,
        FixedTimestep? timestep = null,
        FakeTextInputService? text = null) =>
        new(console, input, text ?? new FakeTextInputService(), new SystemPipeline(), timestep ?? new FixedTimestep(FixedTimestep.DefaultStep), new FakeRenderer());

    /// <summary>Reports a pointer at the origin and the key that a test pressed for one frame.</summary>
    private sealed class FakeInputService : IInputService
    {
        private readonly HashSet<Key> _pressed = [];

        public Vector2 MousePosition => Vector2.Zero;

        /// <summary>Records a key as pressed for the frame that is open, which is how a device reports a transition.</summary>
        public void Press(Key key) => _pressed.Add(key);

        public void BeginFrame() => _pressed.Clear();

        public bool IsKeyDown(Key key) => _pressed.Contains(key);

        public bool IsKeyPressed(Key key) => _pressed.Contains(key);

        public bool IsMouseButtonDown(MouseButton button) => false;

        public bool IsMouseButtonPressed(MouseButton button) => false;
    }

    /// <summary>Reports the characters of one frame, which is what a service that reads a device reports.</summary>
    private sealed class FakeTextInputService : ITextInputService
    {
        private string _typed = string.Empty;

        /// <summary>Sets the characters that the next read reports, as if they were typed during that frame.</summary>
        public void Type(string characters) => _typed = characters;

        public string TypedCharacters
        {
            get
            {
                string typed = _typed;
                _typed = string.Empty;
                return typed;
            }
        }
    }

    /// <summary>The renderer the overlay is built with, which the tests of the keys never draw through.</summary>
    private sealed class FakeRenderer : IRenderer
    {
        public Vector2 ViewportSize => new(1280f, 720f);

        public void Attach(IWindowService window) => throw new NotSupportedException();

        public void SetCamera(Camera2D camera) => throw new NotSupportedException();

        public void BeginFrame(bool clear) => throw new NotSupportedException();

        public void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color, float rotation = 0f) => throw new NotSupportedException();

        public void DrawTextureRegion(TextureHandle texture, Rect source, Vector2 position, Vector2 size, Color color, float rotation = 0f) => throw new NotSupportedException();

        public TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height) => throw new NotSupportedException();

        public void ReleaseTexture(TextureHandle texture) => throw new NotSupportedException();

        public void DrawRectangle(Rect rect, Color color) => throw new NotSupportedException();

        public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color) => throw new NotSupportedException();

        public void EndFrame() => throw new NotSupportedException();

        public void Dispose() => throw new NotSupportedException();
    }
}
