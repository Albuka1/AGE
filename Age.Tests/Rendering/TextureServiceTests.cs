using Age.Assets;
using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Age.Tests;

public sealed class TextureServiceTests
{
    [Fact]
    public void TextureService_LoadTwice_UploadsOnceAndReturnsTheSameHandle()
    {
        var images = new FakeImageLoader();
        var renderer = new FakeRenderer();
        var textures = new TextureService(images, renderer);

        TextureHandle first = textures.Load("art/tile.bmp");
        TextureHandle second = textures.Load("art/tile.bmp");

        images.Calls.Should().Be(1);
        renderer.Created.Should().HaveCount(1);
        second.Should().Be(first);
        textures.Count.Should().Be(1);
    }

    [Fact]
    public void TextureService_Load_UploadsTheDecodedSize()
    {
        var renderer = new FakeRenderer();
        var textures = new TextureService(new FakeImageLoader(), renderer);

        textures.Load("art/tile.bmp");

        renderer.LastWidth.Should().Be(2);
        renderer.LastHeight.Should().Be(2);
        renderer.LastPixelCount.Should().Be(16);
    }

    [Fact]
    public void TextureService_Unload_ReleasesTheTextureAndInvalidatesTheHandle()
    {
        var renderer = new FakeRenderer();
        var textures = new TextureService(new FakeImageLoader(), renderer);
        TextureHandle handle = textures.Load("art/tile.bmp");

        bool unloaded = textures.Unload(handle);

        unloaded.Should().BeTrue();
        renderer.Released.Should().Equal(renderer.Created);
        textures.IsAlive(handle).Should().BeFalse();
        textures.Count.Should().Be(0);
        textures.Unload(handle).Should().BeFalse();
    }

    [Fact]
    public void TextureService_LoadAfterUnload_UploadsAgain()
    {
        var images = new FakeImageLoader();
        var renderer = new FakeRenderer();
        var textures = new TextureService(images, renderer);
        TextureHandle first = textures.Load("art/tile.bmp");
        textures.Unload(first);

        TextureHandle second = textures.Load("art/tile.bmp");

        images.Calls.Should().Be(2);
        renderer.Created.Should().HaveCount(2);
        second.Should().NotBe(first);
        textures.IsAlive(first).Should().BeFalse();
        textures.IsAlive(second).Should().BeTrue();
    }

    [Fact]
    public void TextureService_IsAlive_IsFalseForAHandleTheRendererMade()
    {
        var textures = new TextureService(new FakeImageLoader(), new FakeRenderer());

        textures.IsAlive(new TextureHandle(7)).Should().BeFalse();
    }

    [Fact]
    public void TextureService_UnloadAll_ReleasesEveryTexture()
    {
        var renderer = new FakeRenderer();
        var textures = new TextureService(new FakeImageLoader(), renderer);
        textures.Load("art/a.bmp");
        textures.Load("art/b.bmp");

        textures.UnloadAll();

        renderer.Released.Should().Equal(renderer.Created);
        textures.Count.Should().Be(0);
    }

    [Fact]
    public void TextureService_Dispose_ReleasesEveryTexture()
    {
        var renderer = new FakeRenderer();
        var textures = new TextureService(new FakeImageLoader(), renderer);
        textures.Load("art/a.bmp");

        textures.Dispose();

        renderer.Released.Should().HaveCount(1);
        textures.Count.Should().Be(0);
    }

    [Fact]
    public void TextureService_UnloadAll_WhenAReleaseFails_KeepsThatTextureAndUnloadsTheRest()
    {
        var renderer = new FakeRenderer { FailRelease = id => id == 1 };
        var textures = new TextureService(new FakeImageLoader(), renderer);
        TextureHandle first = textures.Load("art/a.bmp");
        TextureHandle second = textures.Load("art/b.bmp");

        Action unloadAll = textures.UnloadAll;

        unloadAll.Should().Throw<InvalidOperationException>();
        renderer.Released.Should().Equal(1, 2);
        textures.Count.Should().Be(1);
        textures.IsAlive(first).Should().BeTrue();
        textures.IsAlive(second).Should().BeFalse();
    }

    [Fact]
    public void TextureService_UnloadAll_AfterAFailedRelease_RetriesTheTexture()
    {
        var renderer = new FakeRenderer { FailRelease = id => id == 1 };
        var textures = new TextureService(new FakeImageLoader(), renderer);
        TextureHandle handle = textures.Load("art/a.bmp");
        Action firstAttempt = textures.UnloadAll;
        firstAttempt.Should().Throw<InvalidOperationException>();

        renderer.FailRelease = null;
        textures.UnloadAll();

        renderer.Released.Should().Equal(1, 1);
        textures.Count.Should().Be(0);
        textures.IsAlive(handle).Should().BeFalse();
    }

    [Fact]
    public void TextureService_Load_WhenThePathIsTakenWhileDecoding_ReleasesTheUploadItMade()
    {
        var renderer = new FakeRenderer();
        var images = new ReentrantImageLoader();
        var textures = new TextureService(images, renderer);
        images.Service = textures;

        Action load = () => textures.Load("art/tile.bmp");

        load.Should().Throw<InvalidOperationException>();
        renderer.Created.Should().Equal(1, 2);
        renderer.Released.Should().Equal(2);
        textures.Count.Should().Be(1);
    }

    [Fact]
    public void TextureService_Resolve_AnImageThatIsNotThere_DrawsThePlaceholderAndReportsItOnce()
    {
        const string Gone = "Textures/Nowhere/gone.png";
        var images = new FakeImageLoader { FailFor = Gone };
        var logger = new RecordingLogger<TextureService>();
        var textures = new TextureService(images, new FakeRenderer(), logger);

        TextureHandle first = textures.Resolve(Gone);
        TextureHandle second = textures.Resolve(Gone);

        first.Should().Be(textures.Error, "an image that is not there is drawn as the placeholder");
        second.Should().Be(first);
        images.Calls.Should().Be(1, "a path that is broken is asked for once and known from then on");
        textures.MissingCount.Should().Be(1);
        textures.Missing.Should().Equal(Gone);
        logger.Records.Should().ContainSingle(record => record.Level == LogLevel.Error && record.Message.Contains(Gone, StringComparison.Ordinal), "a missing image is reported once rather than on every frame");
    }

    [Fact]
    public void TextureService_Resolve_AnImageThatIsThere_UploadsItOnceAndKeepsTheHandle()
    {
        var images = new FakeImageLoader();
        var renderer = new FakeRenderer();
        var textures = new TextureService(images, renderer);

        TextureHandle handle = textures.Resolve("art/tile.bmp");

        textures.IsAlive(handle).Should().BeTrue("the image is uploaded rather than replaced by the placeholder");
        textures.Resolve("art/tile.bmp").Should().Be(handle);
        images.Calls.Should().Be(1);
        textures.MissingCount.Should().Be(0);
    }

    [Fact]
    public void TextureService_Error_IsACheckerboardThatSaysWhatItIs()
    {
        var renderer = new FakeRenderer();
        var textures = new TextureService(new FakeImageLoader(), renderer);

        TextureHandle error = textures.Error;

        renderer.Created.Should().ContainSingle("the placeholder is built once and kept");
        renderer.LastWidth.Should().Be(64);
        renderer.LastHeight.Should().Be(64);
        HasPixel(renderer.LastPixels!, 255, 0, 255).Should().BeTrue("the placeholder is a checkerboard of magenta");
        HasPixel(renderer.LastPixels!, 32, 0, 32).Should().BeTrue("which alternates with a near black");
        HasPixel(renderer.LastPixels!, 255, 255, 255).Should().BeTrue("and it spells out what it is");
        textures.Error.Should().Be(error);
    }

    [Fact]
    public void TextureService_UnloadAll_ForgetsThePlaceholderSoTheNextCallBuildsItAgain()
    {
        var renderer = new FakeRenderer();
        var textures = new TextureService(new FakeImageLoader(), renderer);
        TextureHandle before = textures.Error;

        textures.UnloadAll();

        textures.Error.Should().NotBe(before, "the renderer owns the placeholder and deletes it with its own objects");
        renderer.Created.Should().HaveCount(2);
    }

    /// <summary>Determines whether one pixel of a buffer holds a colour, which is how a test reads a built texture.</summary>
    private static bool HasPixel(byte[] pixels, byte red, byte green, byte blue) =>
        Enumerable.Range(0, pixels.Length / 4).Any(index =>
            pixels[index * 4] == red && pixels[(index * 4) + 1] == green && pixels[(index * 4) + 2] == blue && pixels[(index * 4) + 3] == 255);

    /// <summary>Keeps what was logged, which is how a test reads the record of an image that is not there.</summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Records { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Records.Add((logLevel, formatter(state, exception)));
    }

    private sealed class FakeImageLoader : IImageLoader
    {
        public int Calls { get; private set; }

        public string? FailFor { get; init; }

        public ImageData Load(string relativePath)
        {
            Calls++;

            if (relativePath == FailFor)
            {
                throw new FileNotFoundException($"There is no image at '{relativePath}'.");
            }

            return new ImageData(2, 2, new byte[16]);
        }
    }

    /// <summary>An image loader that loads the same path through the service once, which registers that path while the outer call is still decoding.</summary>
    private sealed class ReentrantImageLoader : IImageLoader
    {
        private bool _reentered;

        public TextureService? Service { get; set; }

        public ImageData Load(string relativePath)
        {
            if (!_reentered && Service is not null)
            {
                _reentered = true;
                Service.Load(relativePath);
            }

            return new ImageData(2, 2, new byte[16]);
        }
    }

    private sealed class FakeRenderer : IRenderer
    {
        public List<int> Created { get; } = [];
        public List<int> Released { get; } = [];
        public int LastWidth { get; private set; }
        public int LastHeight { get; private set; }
        public int LastPixelCount { get; private set; }

        public byte[]? LastPixels { get; private set; }

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

        public void DrawRectangle(Rect rect, Color color)
        {
        }

        public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color)
        {
        }

        public void EndFrame()
        {
        }

        public TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height)
        {
            LastWidth = width;
            LastHeight = height;
            LastPixelCount = pixels.Length;
            LastPixels = pixels.ToArray();

            int id = Created.Count + 1;
            Created.Add(id);
            return new TextureHandle(id);
        }

        public Func<int, bool>? FailRelease { get; set; }

        public void Dispose()
        {
        }

        public void ReleaseTexture(TextureHandle texture)
        {
            Released.Add(texture.Id);

            if (FailRelease?.Invoke(texture.Id) == true)
            {
                throw new InvalidOperationException($"The renderer refused to release texture {texture.Id}.");
            }
        }
    }
}
