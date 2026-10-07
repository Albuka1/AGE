using Age.Core;
using Age.Input;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class SplashScreenTests
{
    [Fact]
    public void SplashScreen_Draw_UploadsTheBuiltInLogoAndEndsWithTheDuration()
    {
        var renderer = new FakeRenderer();
        var splash = new SplashScreen { Duration = TimeSpan.FromSeconds(2) };

        bool first = splash.Draw(renderer, new GameTime(0.016, 0));
        bool during = splash.Draw(renderer, new GameTime(0.016, 1.5));
        bool after = splash.Draw(renderer, new GameTime(0.016, 2));

        first.Should().BeTrue();
        during.Should().BeTrue();
        after.Should().BeFalse();
        renderer.Created.Should().HaveCount(1);
        renderer.LastWidth.Should().Be(320);
        renderer.LastHeight.Should().Be(320);
        renderer.LastPixelCount.Should().Be(320 * 320 * 4);
    }

    [Fact]
    public void SplashScreen_Draw_DrawsTheLogoInTheCentreOfAWhiteClearedFrame()
    {
        var renderer = new FakeRenderer();
        var splash = new SplashScreen();

        splash.Draw(renderer, new GameTime(0.016, 0));

        renderer.Drawn.Should().HaveCount(1);
        renderer.LastPosition.Should().Be(new Vector2((1280f - 320f) / 2f, (720f - 320f) / 2f));
        renderer.LastSize.Should().Be(new Vector2(320f, 320f));
        renderer.LastColor.Should().Be(Color.White);
        renderer.Cleared.Should().BeTrue();
        renderer.FrameEnded.Should().BeTrue();
    }

    [Fact]
    public void SplashScreen_Draw_DoesNothingWhenItIsDisabled()
    {
        var renderer = new FakeRenderer();
        var splash = new SplashScreen { Enabled = false };

        bool drawn = splash.Draw(renderer, new GameTime(0.016, 0));

        drawn.Should().BeFalse();
        renderer.Created.Should().BeEmpty();
        renderer.Drawn.Should().BeEmpty();
    }

    [Fact]
    public void SplashScreen_Draw_UsesTheLogoOfTheCaller()
    {
        var renderer = new FakeRenderer();
        var logo = new TextureHandle(7);
        var splash = new SplashScreen { Logo = logo, LogoSize = new Vector2(64f, 48f) };

        splash.Draw(renderer, new GameTime(0.016, 0));

        renderer.Created.Should().BeEmpty();
        renderer.Drawn.Should().Equal(7);
        renderer.LastSize.Should().Be(new Vector2(64f, 48f));
    }

    [Theory]
    [InlineData(Key.Space)]
    [InlineData(Key.Enter)]
    [InlineData(Key.Escape)]
    public void SplashScreen_Draw_EndsOnAKeyPress(Key key)
    {
        var renderer = new FakeRenderer();
        var splash = new SplashScreen();
        var input = new FakeInput { PressedKeys = [key] };

        bool skipped = splash.Draw(renderer, new GameTime(0.016, 0), input);
        bool again = splash.Draw(renderer, new GameTime(0.032, 0.016), input);

        skipped.Should().BeTrue("the frame with the skip request still belongs to the splash");
        again.Should().BeFalse();
        renderer.Created.Should().HaveCount(1);
        renderer.Drawn.Should().HaveCount(1);
    }

    [Fact]
    public void SplashScreen_Draw_EndsOnAClick()
    {
        var renderer = new FakeRenderer();
        var splash = new SplashScreen();
        var input = new FakeInput { PressedButtons = [MouseButton.Left] };

        splash.Draw(renderer, new GameTime(0.016, 0), input).Should().BeTrue("the frame with the skip request still belongs to the splash");
        splash.Draw(renderer, new GameTime(0.032, 0.016), input).Should().BeFalse();
    }

    [Fact]
    public void SplashScreen_Draw_IgnoresInputWhenSkipOnInputIsOff()
    {
        var renderer = new FakeRenderer();
        var splash = new SplashScreen { SkipOnInput = false };
        var input = new FakeInput { PressedKeys = [Key.Space] };

        splash.Draw(renderer, new GameTime(0.016, 0), input).Should().BeTrue();
    }

    [Fact]
    public void SplashScreen_End_EndsTheSplashBeforeTheDurationRanOut()
    {
        var renderer = new FakeRenderer();
        var splash = new SplashScreen { Duration = TimeSpan.FromSeconds(10) };
        splash.Draw(renderer, new GameTime(0.016, 0));

        splash.End();

        splash.Draw(renderer, new GameTime(0.016, 0.5)).Should().BeFalse();
        renderer.Drawn.Should().HaveCount(1);
    }

    [Fact]
    public void SplashScreen_Dispose_ReleasesTheBuiltInLogo()
    {
        var renderer = new FakeRenderer();
        var splash = new SplashScreen();
        splash.Draw(renderer, new GameTime(0.016, 0));

        splash.Dispose();

        renderer.Released.Should().Equal(renderer.Created);
    }

    [Fact]
    public void SplashScreen_Dispose_KeepsTheLogoOfTheCaller()
    {
        var renderer = new FakeRenderer();
        var splash = new SplashScreen { Logo = new TextureHandle(7) };
        splash.Draw(renderer, new GameTime(0.016, 0));

        splash.Dispose();

        renderer.Released.Should().BeEmpty();
    }

    private sealed class FakeRenderer : IRenderer
    {
        public List<int> Created { get; } = [];
        public List<int> Released { get; } = [];
        public List<int> Drawn { get; } = [];
        public int LastWidth { get; private set; }
        public int LastHeight { get; private set; }
        public int LastPixelCount { get; private set; }
        public Vector2 LastPosition { get; private set; }
        public Vector2 LastSize { get; private set; }
        public Color LastColor { get; private set; }
        public float LastRotation { get; private set; }
        public Rect LastSource { get; private set; }
        public bool Cleared { get; private set; }
        public bool FrameEnded { get; private set; }

        public Vector2 ViewportSize => new(1280f, 720f);

        public void Attach(IWindowService window)
        {
        }

        public void SetCamera(Camera2D camera)
        {
        }

        public void BeginFrame(bool clear) => Cleared |= clear;

        public void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color, float rotation = 0f)
        {
            Drawn.Add(texture.Id);
            LastPosition = position;
            LastSize = size;
            LastColor = color;
            LastRotation = rotation;
        }

        public void DrawTextureRegion(TextureHandle texture, Rect source, Vector2 position, Vector2 size, Color color, float rotation = 0f)
        {
            LastSource = source;
            DrawSprite(texture, position, size, color, rotation);
        }

        public void DrawRectangle(Rect rect, Color color)
        {
        }

        public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color)
        {
        }

        public void EndFrame() => FrameEnded = true;

        public TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height)
        {
            LastWidth = width;
            LastHeight = height;
            LastPixelCount = pixels.Length;

            int id = Created.Count + 1;
            Created.Add(id);
            return new TextureHandle(id);
        }

        public void ReleaseTexture(TextureHandle texture) => Released.Add(texture.Id);

        public void Dispose()
        {
        }
    }

    private sealed class FakeInput : IInputService
    {
        public IReadOnlyCollection<Key> PressedKeys { get; init; } = [];
        public IReadOnlyCollection<MouseButton> PressedButtons { get; init; } = [];

        public Vector2 MousePosition => Vector2.Zero;

        public void BeginFrame()
        {
        }

        public bool IsKeyDown(Key key) => false;

        public bool IsKeyPressed(Key key) => PressedKeys.Contains(key);

        public bool IsMouseButtonDown(MouseButton button) => false;

        public bool IsMouseButtonPressed(MouseButton button) => PressedButtons.Contains(button);
    }
}
