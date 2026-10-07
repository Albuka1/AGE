using Age.Assets;
using Age.Core;
using Age.Rendering;
using FluentAssertions;
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

    private sealed class FakeImageLoader : IImageLoader
    {
        public int Calls { get; private set; }

        public ImageData Load(string relativePath)
        {
            Calls++;
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

        public void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color)
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

            int id = Created.Count + 1;
            Created.Add(id);
            return new TextureHandle(id);
        }

        public void ReleaseTexture(TextureHandle texture) => Released.Add(texture.Id);
    }
}
