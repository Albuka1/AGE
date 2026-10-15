using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

/// <summary>
/// Pins the display settings of a game to the window: the three names are the ones a person types in the console, the
/// values of the window are what they start at, and a read writes them back without a device.
/// </summary>
public sealed class WindowCVarsTests
{
    [Fact]
    public void WindowCVars_Register_TakesTheValuesTheWindowAlreadyCarries()
    {
        var window = new FakeWindowService { Fullscreen = true, VSync = false, FpsLimit = 60 };
        var cvars = new CVarService();

        WindowCVars.Register(cvars, window);

        cvars.Get<bool>(WindowCVars.Fullscreen).Should().BeTrue();
        cvars.Get<bool>(WindowCVars.VSync).Should().BeFalse();
        cvars.Get<int>(WindowCVars.Fps).Should().Be(60);
    }

    [Fact]
    public void WindowCVars_Pull_WritesTheValuesOfTheSettingsToTheWindow()
    {
        var window = new FakeWindowService();
        var cvars = new CVarService();
        WindowCVars.Register(cvars, window);

        cvars.SetFromText(WindowCVars.Fullscreen, "true");
        cvars.SetFromText(WindowCVars.VSync, "false");
        cvars.SetFromText(WindowCVars.Fps, "144");

        WindowCVars.Pull(cvars, window);

        window.Fullscreen.Should().BeTrue();
        window.VSync.Should().BeFalse();
        window.FpsLimit.Should().Be(144);
    }

    [Fact]
    public void WindowCVars_Pull_IsIdempotent()
    {
        var window = new FakeWindowService { VSync = true, FpsLimit = 0 };
        var cvars = new CVarService();
        WindowCVars.Register(cvars, window);

        // A game reads the settings on every frame and writes what it read: a second read that finds what it left writes
        // nothing that changed, which is what the window itself is relied on to answer.
        int before = window.FullscreenWrites + window.VSyncWrites + window.FpsWrites;

        WindowCVars.Pull(cvars, window);
        WindowCVars.Pull(cvars, window);

        (window.FullscreenWrites + window.VSyncWrites + window.FpsWrites).Should().BeGreaterThan(before);
        window.Fullscreen.Should().BeFalse();
        window.VSync.Should().BeTrue();
        window.FpsLimit.Should().Be(0);
    }

    [Fact]
    public void WindowCVars_RegisterAndPull_NullArgumentsAreRefused()
    {
        var window = new FakeWindowService();
        var cvars = new CVarService();

        FluentActions.Invoking(() => WindowCVars.Register(null!, window)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => WindowCVars.Register(cvars, null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => WindowCVars.Pull(null!, window)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => WindowCVars.Pull(cvars, null!)).Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// The window of a test: it holds what a game writes to it and counts how often each member was written, which is
    /// what a test reads to say that a value was carried rather than read and thrown away.
    /// </summary>
    private sealed class FakeWindowService : IWindowService
    {
        private bool _fullscreen;
        private bool _vsync;
        private int _fps;

        public Silk.NET.Windowing.IWindow Window => throw new NotSupportedException("A test does not open a window of the operating system.");

        public string Title { get; set; } = string.Empty;

        public bool Fullscreen
        {
            get => _fullscreen;
            set
            {
                _fullscreen = value;
                FullscreenWrites++;
            }
        }

        public bool VSync
        {
            get => _vsync;
            set
            {
                _vsync = value;
                VSyncWrites++;
            }
        }

        public int FpsLimit
        {
            get => _fps;
            set
            {
                _fps = value < 0 ? 0 : value;
                FpsWrites++;
            }
        }

        public int RefreshRate => 60;

        public int FullscreenWrites { get; private set; }
        public int VSyncWrites { get; private set; }
        public int FpsWrites { get; private set; }

        public void Create(int width, int height, string title) => throw new NotSupportedException("A test does not open a window of the operating system.");

        public void SetIcon(ReadOnlySpan<byte> pixels, int width, int height) => throw new NotSupportedException("A test does not set an icon of a window of the operating system.");

        public void Close()
        {
        }

        public void Apply()
        {
        }
    }
}
