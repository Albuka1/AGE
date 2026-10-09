using Age.Assets;
using Age.Audio;
using Age.Core;
using Age.Input;
using Age.Rendering;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Age.Tests;

public sealed class GameShutdownTests
{
    [Fact]
    public void GameShutdown_Run_RunsTheStepsFromTheLowestOrderToTheHighest()
    {
        var log = new List<string>();
        var shutdown = new GameShutdown(
        [
            new FakeStep(30, "third", log),
            new FakeStep(10, "first", log),
            new FakeStep(20, "second", log),
        ]);

        shutdown.Count.Should().Be(3);
        shutdown.Run();

        log.Should().Equal("first", "second", "third");
    }

    [Fact]
    public void GameShutdown_Run_IsIdempotent()
    {
        var log = new List<string>();
        var shutdown = new GameShutdown([new FakeStep(10, "once", log)]);

        shutdown.Run();
        shutdown.Run();

        log.Should().Equal("once");
    }

    [Fact]
    public void GameShutdown_Run_RunsEveryStepEvenWhenOneFails()
    {
        var log = new List<string>();
        var shutdown = new GameShutdown(
        [
            new FakeStep(10, "first", log),
            new FakeStep(20, "second", log, new InvalidOperationException("the step failed")),
            new FakeStep(30, "third", log),
        ]);

        Action run = shutdown.Run;

        run.Should().Throw<InvalidOperationException>().WithMessage("the step failed");
        log.Should().Equal("first", "second", "third");
    }

    [Fact]
    public void GameShutdown_Run_WithoutSteps_DoesNothing()
    {
        var shutdown = new GameShutdown([]);

        shutdown.Count.Should().Be(0);
        shutdown.Run();
    }

    [Fact]
    public void GameShutdown_StepsOfTheEngine_ComeFromEveryAssemblyThatOwnsADeviceObject()
    {
        GameShutdown shutdown = new ServiceCollection()
            .AddAgeCore()
            .AddAgeAssets()
            .AddAgeInput()
            .AddAgeAudio()
            .AddAgeRendering()
            .AddAgeSilkInput()
            .BuildServiceProvider()
            .GetRequiredService<GameShutdown>();

        shutdown.Count.Should().Be(3, "the audio assembly releases its samples, the rendering assembly its splash, its textures, its renderer and its window");
    }

    [Fact]
    public void RenderingShutdownStep_Shutdown_ReleasesEverythingEvenWhenOneReleaseFails()
    {
        var splash = new SplashScreen();
        var fonts = new FakeFontService { Failure = new InvalidOperationException("the atlas was not released") };
        var textures = new FakeTextureService();
        var renderer = new FakeRenderer();
        var step = new RenderingShutdownStep(splash, fonts, textures, renderer);

        Action shutdown = step.Shutdown;

        shutdown.Should().Throw<InvalidOperationException>().WithMessage("the atlas was not released");
        fonts.Unloaded.Should().BeTrue();
        textures.Unloaded.Should().BeTrue("a device object behind a release that failed is still released");
        renderer.Disposed.Should().BeTrue("the renderer goes last, whatever happened before it");
    }

    [Fact]
    public void RenderingShutdownStep_Shutdown_WithoutAFailure_ReleasesEverything()
    {
        var fonts = new FakeFontService();
        var textures = new FakeTextureService();
        var renderer = new FakeRenderer();

        new RenderingShutdownStep(new SplashScreen(), fonts, textures, renderer).Shutdown();

        fonts.Unloaded.Should().BeTrue();
        textures.Unloaded.Should().BeTrue();
        renderer.Disposed.Should().BeTrue();
    }

    private sealed class FakeFontService : IFontService
    {
        public Exception? Failure { get; init; }

        public bool Unloaded { get; private set; }

        public int Count => 0;

        public FontHandle Load(string relativePath, float pixelHeight, char first = ' ', char last = '~') => default;

        public bool IsAlive(FontHandle font) => false;

        public bool Unload(FontHandle font) => false;

        public void UnloadAll()
        {
            Unloaded = true;

            if (Failure is not null)
            {
                throw Failure;
            }
        }

        public Vector2 Measure(FontHandle font, ReadOnlySpan<char> text) => default;

        public void Draw(FontHandle font, ReadOnlySpan<char> text, Vector2 position, Color color)
        {
        }
    }

    private sealed class FakeTextureService : ITextureService
    {
        public bool Unloaded { get; private set; }

        public int Count => 0;

        public TextureHandle Error => default;

        public int MissingCount => 0;

        public IEnumerable<string> Missing => [];

        public TextureHandle Resolve(string relativePath) => default;

        public TextureHandle Load(string relativePath) => default;

        public bool IsAlive(TextureHandle texture) => false;

        public bool Unload(TextureHandle texture) => false;

        public void UnloadAll() => Unloaded = true;
    }

    private sealed class FakeRenderer : IRenderer
    {
        public bool Disposed { get; private set; }

        public Vector2 ViewportSize => new(1280f, 720f);

        public void Attach(IWindowService window)
        {
        }

        public void SetCamera(Camera2D camera)
        {
        }

        public void BeginFrame(bool clear)
        {
        }

        public void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color, float rotation = 0f)
        {
        }

        public void DrawTextureRegion(TextureHandle texture, Rect source, Vector2 position, Vector2 size, Color color, float rotation = 0f)
        {
        }

        public TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height) => default;

        public void ReleaseTexture(TextureHandle texture)
        {
        }

        public void DrawRectangle(Rect rect, Color color)
        {
        }

        public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color)
        {
        }

        public void EndFrame()
        {
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class FakeStep : IGameShutdownStep
    {
        private readonly string _name;
        private readonly List<string> _log;
        private readonly Exception? _failure;

        public FakeStep(int order, string name, List<string> log, Exception? failure = null)
        {
            Order = order;
            _name = name;
            _log = log;
            _failure = failure;
        }

        public int Order { get; }

        public void Shutdown()
        {
            _log.Add(_name);

            if (_failure is not null)
            {
                throw _failure;
            }
        }
    }
}
