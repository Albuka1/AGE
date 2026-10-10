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
    public void SplashScreen_End_HoldsTheLogoForTheShortestTime()
    {
        var renderer = new FakeRenderer();
        var splash = new SplashScreen { Duration = TimeSpan.FromSeconds(5), MinimumDuration = TimeSpan.FromSeconds(1) };

        // The loading of a light game finishes on the first frame, which is what asks the splash to end.
        splash.End();

        splash.Draw(renderer, new GameTime(0.016, 0)).Should().BeTrue("the logo stays while the shortest time has not passed");
        splash.Draw(renderer, new GameTime(0.016, 0.5)).Should().BeTrue();
        splash.Draw(renderer, new GameTime(0.016, 1)).Should().BeFalse("the logo goes once the shortest time passed");
    }

    [Fact]
    public void SplashScreen_End_DoesNotOutlastTheDurationWhenTheMinimumIsLonger()
    {
        var renderer = new FakeRenderer();
        var splash = new SplashScreen { Duration = TimeSpan.FromSeconds(1), MinimumDuration = TimeSpan.FromSeconds(5) };

        splash.End();

        splash.Draw(renderer, new GameTime(0.016, 0.5)).Should().BeTrue();
        splash.Draw(renderer, new GameTime(0.016, 1)).Should().BeFalse("the duration is what the game asked for, so the minimum cannot hold the logo past it");
    }

    [Fact]
    public void SplashScreen_AKey_EndsTheLogoEvenBeforeTheShortestTime()
    {
        var renderer = new FakeRenderer();
        var splash = new SplashScreen { Duration = TimeSpan.FromSeconds(5), MinimumDuration = TimeSpan.FromSeconds(2) };
        var input = new FakeInput { PressedKeys = [Key.Space] };

        splash.Draw(renderer, new GameTime(0.016, 0), input).Should().BeTrue("the frame with the skip request still belongs to the splash");
        splash.Draw(renderer, new GameTime(0.016, 0.016), input).Should().BeFalse("a person who skips is not held by the shortest time");
    }

    [Fact]
    public void SplashScreen_Draw_DrawsTheLogoInTheCentreOfAWhiteClearedFrame()
    {
        var renderer = new FakeRenderer();
        var splash = new SplashScreen();

        splash.Draw(renderer, new GameTime(0.016, 0));

        renderer.Drawn.Should().HaveCount(1);

        // The logo of a game that did not size it covers a share of the shorter side of the viewport, so the exact size
        // follows the window: a viewport 720 pixels tall gives a square of 320 pixels.
        renderer.LastSize.X.Should().BeApproximately(320f, 0.001f);
        renderer.LastSize.Y.Should().BeApproximately(320f, 0.001f);
        renderer.LastPosition.X.Should().BeApproximately((1280f - renderer.LastSize.X) / 2f, 0.001f);
        renderer.LastPosition.Y.Should().BeApproximately((720f - renderer.LastSize.Y) / 2f, 0.001f);
        renderer.LastColor.Should().Be(Color.White);
        renderer.Cleared.Should().BeTrue();
        renderer.FrameEnded.Should().BeTrue();
    }

    [Fact]
    public void SplashScreen_Draw_LogoFollowsAResizedWindow()
    {
        var renderer = new FakeRenderer();
        var splash = new SplashScreen();

        splash.Draw(renderer, new GameTime(0.016, 0));
        Vector2 first = renderer.LastSize;

        renderer.ViewportSize = new Vector2(640f, 480f);
        splash.Draw(renderer, new GameTime(0.016, 0.5));

        renderer.LastSize.X.Should().BeApproximately(480f * (320f / 720f), 0.001f, "the shorter side of the window decides the size of the logo");
        renderer.LastSize.Should().NotBe(first, "a logo that nobody sized follows the window instead of living with one size");
        renderer.LastPosition.X.Should().BeApproximately((640f - renderer.LastSize.X) / 2f, 0.001f);
        renderer.LastPosition.Y.Should().BeApproximately((480f - renderer.LastSize.Y) / 2f, 0.001f);
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
    public void SplashScreen_Draw_DrawsATrackAndAFillWhileLoading()
    {
        var renderer = new FakeRenderer();
        var splash = new SplashScreen { Progress = 0.5f };

        splash.Draw(renderer, new GameTime(0.016, 0));

        renderer.Rectangles.Should().HaveCount(2, "the bar is a track with the filled part over it");
        renderer.Rectangles[0].Size.X.Should().BeApproximately(0.9f * renderer.LastSize.X, 0.001f, "the bar is a share of the width of the logo");
        renderer.Rectangles[1].Size.X.Should().BeApproximately(renderer.Rectangles[0].Size.X * 0.5f, 0.001f, "half of the bar is filled at a share of one half");
        renderer.Rectangles[1].Position.Should().Be(renderer.Rectangles[0].Position, "the fill grows from the left of the track");
        renderer.Rectangles[0].Position.Y.Should().BeGreaterThan(renderer.LastPosition.Y + renderer.LastSize.Y, "the bar hangs under the logo");
    }

    [Fact]
    public void SplashScreen_Draw_AShareOfZero_DrawsTheTrackAlone()
    {
        var renderer = new FakeRenderer();
        var splash = new SplashScreen { Progress = 0f };

        splash.Draw(renderer, new GameTime(0.016, 0));

        renderer.Rectangles.Should().ContainSingle("a bar that has not filled yet is the track");
    }

    [Fact]
    public void SplashScreen_Draw_AShareOfOneOrMore_HidesTheBar()
    {
        var renderer = new FakeRenderer();
        var splash = new SplashScreen { Progress = 1f };

        splash.Draw(renderer, new GameTime(0.016, 0));

        renderer.Rectangles.Should().BeEmpty("a full bar is nothing left to load, so the logo stands alone");

        var above = new FakeRenderer();
        var splashAbove = new SplashScreen { Progress = 2f };

        splashAbove.Draw(above, new GameTime(0.016, 0));

        above.Rectangles.Should().BeEmpty("a share above one is a full bar as well");
    }

    [Fact]
    public void SplashScreen_Draw_AShortWindow_KeepsTheBarInsideTheViewport()
    {
        var renderer = new FakeRenderer { ViewportSize = new Vector2(320f, 200f) };
        var splash = new SplashScreen { Progress = 0.5f };

        splash.Draw(renderer, new GameTime(0.016, 0));

        renderer.Rectangles.Should().HaveCount(2, "the bar is a track with the filled part over it");
        renderer.Rectangles[0].Position.Y.Should().BeLessThan(renderer.ViewportSize.Y, "the bar hangs under the logo without falling off the bottom of a short window");
        renderer.Rectangles[0].Position.Y.Should().BeGreaterThanOrEqualTo(0f);
    }

    [Fact]
    public void SplashScreen_Draw_TheProgressBarCanBeTurnedOff()
    {
        var renderer = new FakeRenderer();
        var splash = new SplashScreen { ShowProgress = false, Progress = 0.5f };

        splash.Draw(renderer, new GameTime(0.016, 0));

        renderer.Rectangles.Should().BeEmpty("a splash without a bar draws the logo alone");
        renderer.Drawn.Should().HaveCount(1);
    }

    [Fact]
    public void SplashScreen_End_EndsTheSplashBeforeTheDurationRanOut()
    {
        var renderer = new FakeRenderer();
        var splash = new SplashScreen { Duration = TimeSpan.FromSeconds(10), MinimumDuration = TimeSpan.FromSeconds(0.5) };
        splash.Draw(renderer, new GameTime(0.016, 0));

        splash.End();

        splash.Draw(renderer, new GameTime(0.016, 0.25)).Should().BeTrue("the shortest time of the logo has not passed");
        splash.Draw(renderer, new GameTime(0.016, 0.5)).Should().BeFalse("the end of the loading ends the logo before the duration");
        renderer.Drawn.Should().HaveCount(2);
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
        public List<Rect> Rectangles { get; } = [];
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

        public Vector2 ViewportSize { get; set; } = new(1280f, 720f);

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
            Rectangles.Add(rect);
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
