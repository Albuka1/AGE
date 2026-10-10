using System.Diagnostics.CodeAnalysis;
using Age.Content.Sheets;
using Age.Core;
using Age.Input;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class DevOverlayTests
{
    [Fact]
    public void DevOverlay_Update_ItsKeyHidesTheNumbers()
    {
        var input = new FakeInputService();
        DevOverlay overlay = Create(input);

        overlay.ShowStats.Should().BeTrue();

        Press(overlay, input, overlay.StatsKey);

        overlay.ShowStats.Should().BeFalse();
    }

    [Fact]
    public void DevOverlay_Update_MeasuresTheFramesPerSecondOverAWindow()
    {
        var input = new FakeInputService();
        DevOverlay overlay = Create(input);

        for (var frame = 0; frame < 6; frame++)
        {
            Frame(overlay, input, 0.1d);
        }

        overlay.FramesPerSecond.Should().BeApproximately(10d, 0.001d, "six frames of a tenth of a second are ten frames a second");
    }

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
        FakeInputService input,
        IRenderer? renderer = null,
        ISpriteSheetService? sheets = null) =>
        new(input, new SystemPipeline(), new FixedTimestep(FixedTimestep.DefaultStep), renderer ?? new FakeRenderer(), sheets: sheets);

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

        public float MouseWheel => 0f;
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

    [Fact]
    public void DevOverlay_Render_NamesTheSheetsThatDidNotResolve()
    {
        var renderer = new RecordingRenderer();
        var sheets = new FakeSheetService("Textures/Entities/goblin.yml:idle");

        DevOverlay overlay = Create(new FakeInputService(), renderer: renderer, sheets: sheets);

        overlay.Render(new World(), new Camera2D());

        renderer.Texts.Should().Contain(text =>
            text.Contains("1 sheets that did not resolve", StringComparison.Ordinal)
            && text.Contains("goblin.yml:idle", StringComparison.Ordinal));
    }

    /// <summary>A renderer that keeps the lines that were drawn on it, which is what the numbers of the overlay are.</summary>
    private sealed class RecordingRenderer : IRenderer
    {
        public List<string> Texts { get; } = [];

        public Vector2 ViewportSize => new(1280f, 720f);

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

    /// <summary>A sheet service that reports what a test says did not resolve, which is what the overlay names for a build.</summary>
    private sealed class FakeSheetService(params string[] missing) : ISpriteSheetService
    {
        public int Count => 0;

        public IEnumerable<string> Missing => missing;

        public SpriteSheet Load(string relativePath) => throw new NotSupportedException();

        public SpriteRegion Resolve(string relativePath, string state, int frame) => throw new NotSupportedException();

        public bool TryState(string relativePath, string state, [NotNullWhen(true)] out SpriteSheetState? declared) => throw new NotSupportedException();
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
