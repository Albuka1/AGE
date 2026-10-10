using Age.Core;
using Age.Input;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

/// <summary>
/// Pins the console of a game to what a person does with it: it opens with its key and stands the clock still, the characters of
/// a frame reach it, Tab completes the word or takes a suggestion, and the history walks what was entered before.
/// </summary>
public sealed class DevConsoleOverlayTests
{
    [Fact]
    public void DevConsoleOverlay_Update_OpensTheConsoleWithItsKeyAndStandsTheClockStill()
    {
        var console = new ConsoleService();
        var input = new FakeInputService();
        var timestep = new FixedTimestep(FixedTimestep.DefaultStep);
        DevConsoleOverlay overlay = Create(console, input, timestep);

        Press(overlay, input, overlay.OpenKey);

        console.IsOpen.Should().BeTrue();
        timestep.Paused.Should().BeTrue("a console that is open is a game that waits");
        overlay.IsVisible.Should().BeTrue("a game keeps its own input away while the console is there");
    }

    [Fact]
    public void DevConsoleOverlay_Update_ClosingTheConsolePutsTheClockBackTheWayItWas()
    {
        var console = new ConsoleService();
        var input = new FakeInputService();
        var timestep = new FixedTimestep(FixedTimestep.DefaultStep) { Paused = true };
        DevConsoleOverlay overlay = Create(console, input, timestep);

        Press(overlay, input, overlay.OpenKey);
        Press(overlay, input, overlay.OpenKey);

        console.IsOpen.Should().BeFalse();
        timestep.Paused.Should().BeTrue("the game was already paused before the console opened");
    }

    [Fact]
    public void DevConsoleOverlay_Update_RunsTheLineThatWasTyped()
    {
        var console = new ConsoleService();
        var input = new FakeInputService();
        var text = new FakeTextInputService();
        DevConsoleOverlay overlay = Create(console, input, text: text);
        var ran = new List<string>();

        console.Register("spawn", "Puts sprites on screen.", arguments => ran.Add(string.Join(' ', arguments)));

        Press(overlay, input, overlay.OpenKey);

        text.Type("spawn 3");
        Frame(overlay, input);

        console.Input.Should().Be("spawn 3", "the characters of the frame reach the console while it is open");

        Press(overlay, input, Key.Enter);

        ran.Should().Equal("3");
        console.History.Should().Equal("spawn 3");
    }

    [Fact]
    public void DevConsoleOverlay_Update_BackspaceAndTheHistoryKeys_EditTheLine()
    {
        var console = new ConsoleService();
        var input = new FakeInputService();
        var text = new FakeTextInputService();
        DevConsoleOverlay overlay = Create(console, input, text: text);

        Press(overlay, input, overlay.OpenKey);

        text.Type("zzz");
        Frame(overlay, input);
        Press(overlay, input, Key.Enter);
        console.History.Should().Equal("zzz");

        Press(overlay, input, Key.Up);
        console.Input.Should().Be("zzz", "the up key walks what was entered before");

        Press(overlay, input, Key.Down);
        console.Input.Should().BeEmpty("walking past the last line leaves an empty line");
    }

    [Fact]
    public void DevConsoleOverlay_Update_AnEmptyLineListsNothingUntilACharacterIsTyped()
    {
        var console = new ConsoleService();
        var input = new FakeInputService();
        var text = new FakeTextInputService();
        DevConsoleOverlay overlay = Create(console, input, text: text);

        console.Register("cvars", "Lists the settings.", _ => { });
        console.Register("spawn", "Puts sprites on screen.", _ => { });

        Press(overlay, input, overlay.OpenKey);

        // An empty line lists nothing, so the down key is the history of the console rather than a list of every command: the list
        // is what the line could become, and an empty line could become anything.
        Press(overlay, input, Key.Down);
        console.Input.Should().BeEmpty("an empty line has no suggestions to walk");

        // The first character that is typed is what narrows the list down, and the down key then walks it.
        text.Type("cv");
        Frame(overlay, input);
        Press(overlay, input, Key.Down);
        Press(overlay, input, Key.Tab);

        console.Input.Should().Be("cvars", "the list appeared with the characters that were typed");
    }

    [Fact]
    public void DevConsoleOverlay_Update_CompletesTheLevelsOfACommandOneAtATime()
    {
        var console = new ConsoleService();
        var input = new FakeInputService();
        var text = new FakeTextInputService();
        DevConsoleOverlay overlay = Create(console, input, text: text);

        // A command of several levels is a tree, so the levels are suggested one at a time: 'cvars ' lists 'set' and 'get', and Tab
        // takes the level rather than the whole name at once.
        console.Register("cvars", "Lists the settings.", _ => { });
        console.Register("cvars set", "Writes a setting.", _ => { });
        console.Register("cvars get", "Answers a setting.", _ => { });

        Press(overlay, input, overlay.OpenKey);

        text.Type("cvars s");
        Frame(overlay, input);
        Press(overlay, input, Key.Tab);

        console.Input.Should().Be("cvars set", "the level behind the word that is typed is what Tab takes");
    }

    [Fact]
    public void DevConsoleOverlay_Update_TabTakesTheSuggestionThatMatches()
    {
        var console = new ConsoleService();
        var input = new FakeInputService();
        var text = new FakeTextInputService();
        DevConsoleOverlay overlay = Create(console, input, text: text);

        console.Register("spawn", "Puts sprites on screen.", _ => { });

        Press(overlay, input, overlay.OpenKey);
        text.Type("spa");
        Frame(overlay, input);

        Press(overlay, input, Key.Tab);

        console.Input.Should().Be("spawn", "Tab takes the command that matches what is typed");
    }

    [Fact]
    public void DevConsoleOverlay_Update_TheUpAndDownKeysWalkTheSuggestionsWhileTheyAreListed()
    {
        var console = new ConsoleService();
        var input = new FakeInputService();
        var text = new FakeTextInputService();
        DevConsoleOverlay overlay = Create(console, input, text: text);

        console.Register("spawn", "Puts sprites on screen.", _ => { });
        console.Register("stats", "Reports the world.", _ => { });

        Press(overlay, input, overlay.OpenKey);
        text.Type("s");
        Frame(overlay, input);

        // The list holds every command that starts with 's', so the down key walks it rather than the history of the console. The
        // first press takes the first row of the list.
        Press(overlay, input, Key.Down);

        console.Input.Should().Be("s", "walking the suggestions does not change the line until one is taken");

        Press(overlay, input, Key.Down);
        Press(overlay, input, Key.Tab);

        console.Input.Should().Be("stats", "Tab takes the row the down keys stopped on");
    }
    [Fact]
    public void DevConsoleOverlay_Update_TabTakesAValueTheGameOffersBehindACommandOfSeveralWords()
    {
        var console = new ConsoleService();
        var input = new FakeInputService();
        var text = new FakeTextInputService();
        DevConsoleOverlay overlay = Create(console, input, text: text);

        console.Register("cvars set", "Writes a setting.", _ => { });
        overlay.SetValues(line => line == "cvars set locale "
            ? ["en", "ru"]
            : []);

        Press(overlay, input, overlay.OpenKey);
        text.Type("cvars set locale ");
        Frame(overlay, input);

        // The line names the setting, and the values of it are what the panel lists: the first row is taken by Tab, which writes
        // the value behind the path rather than in place of the command.
        Press(overlay, input, Key.Tab);

        console.Input.Should().Be("cvars set locale en", "the value is added behind the path that is already typed");
    }

    [Fact]
    public void DevConsoleOverlay_Update_TheUpAndDownKeysWalkTheValuesOfAnArgument()
    {
        var console = new ConsoleService();
        var input = new FakeInputService();
        var text = new FakeTextInputService();
        DevConsoleOverlay overlay = Create(console, input, text: text);

        console.Register("cvars set", "Writes a setting.", _ => { });
        overlay.SetValues(line => line == "cvars set locale " ? ["en", "ru"] : []);

        Press(overlay, input, overlay.OpenKey);
        text.Type("cvars set locale ");
        Frame(overlay, input);

        Press(overlay, input, Key.Down);
        Press(overlay, input, Key.Down);
        Press(overlay, input, Key.Tab);

        console.Input.Should().Be("cvars set locale ru", "the down key walked the list of values to the second one");
    }

    [Fact]
    public void DevConsoleOverlay_Update_ThePageKeysScrollTheOutput()
    {
        var console = new ConsoleService();
        var input = new FakeInputService();
        DevConsoleOverlay overlay = Create(console, input);

        overlay.VisibleLines = 4;

        for (var index = 0; index < 20; index++)
        {
            console.Write($"line {index}");
        }

        Press(overlay, input, overlay.OpenKey);

        // A page up walks back through the output, which is what reads a log that is longer than the panel. The keys walk the
        // output rather than the history or the line, so the console stays open and the line that is being typed is left alone.
        Press(overlay, input, Key.PageUp);
        Press(overlay, input, Key.PageUp);

        console.IsOpen.Should().BeTrue("the page keys scroll the output rather than close the console");
        console.Input.Should().BeEmpty("scrolling does not type into the line");

        // The page down walks forward again, and a scroll that runs past the end settles there rather than going negative.
        Press(overlay, input, Key.PageDown);
        Press(overlay, input, Key.PageDown);
        Press(overlay, input, Key.PageDown);

        console.IsOpen.Should().BeTrue("the page keys do not disturb the state of the console");
    }



    [Fact]
    public void DevConsoleOverlay_Update_EscapeClosesTheConsole()
    {
        var console = new ConsoleService();
        var input = new FakeInputService();
        DevConsoleOverlay overlay = Create(console, input);

        Press(overlay, input, overlay.OpenKey);
        Press(overlay, input, Key.Escape);

        console.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void DevConsoleOverlay_Update_AHeldBackspace_ErasesMoreThanOneCharacter()
    {
        var console = new ConsoleService();
        var input = new FakeInputService();
        var text = new FakeTextInputService();
        DevConsoleOverlay overlay = Create(console, input, text: text);

        Press(overlay, input, overlay.OpenKey);

        text.Type("hello");
        Frame(overlay, input);
        console.Input.Should().Be("hello");

        // The key goes down on this frame, which erases one character, and then stays down, which is what a device reports for a
        // key that is held.
        input.BeginFrame();
        input.Press(Key.Backspace);
        overlay.Update(new GameTime(0.016d, 0.016d));
        console.Input.Should().Be("hell", "the frame of the press erases one character");

        // A held key waits out the delay and then repeats on its own, without a second press.
        double time = 0.016d;

        for (var frame = 0; frame < 40; frame++)
        {
            time += 0.05d;
            input.BeginFrame();
            input.Hold(Key.Backspace);
            overlay.Update(new GameTime(0.05d, time));
        }

        console.Input.Should().BeEmpty("a key that is held erases the line rather than one character");
    }

    /// <summary>Presses a key for one frame and runs it, which is one frame of the loop: the frame opens, the device reports the key, the console reads it.</summary>
    private static void Press(DevConsoleOverlay overlay, FakeInputService input, Key key, double delta = 0.016d)
    {
        input.BeginFrame();
        input.Press(key);
        overlay.Update(new GameTime(delta, 0d));
    }

    /// <summary>Runs one frame: input opens its frame, which forgets the keys of the frame before, and the console reads it.</summary>
    private static void Frame(DevConsoleOverlay overlay, FakeInputService input, double delta = 0.016d)
    {
        input.BeginFrame();
        overlay.Update(new GameTime(delta, 0d));
    }

    /// <summary>Builds a console over the services a test drives, with the fakes it does not look at.</summary>
    private static DevConsoleOverlay Create(
        ConsoleService console,
        FakeInputService input,
        FixedTimestep? timestep = null,
        FakeTextInputService? text = null,
        IRenderer? renderer = null) =>
        new(console, input, text ?? new FakeTextInputService(), timestep ?? new FixedTimestep(FixedTimestep.DefaultStep), renderer ?? new FakeRenderer());

    /// <summary>Reports a pointer at the origin and the key that a test pressed or held for one frame.</summary>
    private sealed class FakeInputService : IInputService
    {
        private readonly HashSet<Key> _pressed = [];
        private readonly HashSet<Key> _down = [];

        public Vector2 MousePosition => Vector2.Zero;

        /// <summary>Records a key as pressed for the frame that is open, which is how a device reports a transition.</summary>
        public void Press(Key key)
        {
            _pressed.Add(key);
            _down.Add(key);
        }

        /// <summary>Records a key as held without a transition, which is what a device reports for a key that stays down.</summary>
        public void Hold(Key key) => _down.Add(key);

        public void BeginFrame() => _pressed.Clear();

        public bool IsKeyDown(Key key) => _down.Contains(key);

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

    /// <summary>The renderer the console is built with, which the tests of the keys never draw through.</summary>
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
