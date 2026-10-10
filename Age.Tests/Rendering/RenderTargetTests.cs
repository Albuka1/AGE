using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

/// <summary>
/// Pins what a render target is to a caller: a handle that carries the texture and the size of the surface it names, and a
/// renderer that draws into the window only refusing to make one. What a device does with a target needs a window, so the part
/// of it that a test can hold is the contract of the seam.
/// </summary>
public sealed class RenderTargetTests
{
    [Fact]
    public void RenderTargetHandle_CarriesTheTextureTheSizeAndTheAttachmentOfTheSurface()
    {
        var target = new RenderTargetHandle(framebuffer: 7, texture: 42, size: new Vector2(320f, 200f), generation: 3);

        target.Texture.Id.Should().Be(42);
        target.Size.Should().Be(new Vector2(320f, 200f));
        target.Generation.Should().Be(3, "the handle says which attachment of the renderer created the target");
    }

    [Fact]
    public void RenderTargetHandle_TwoHandlesOfOneTarget_AreEqual()
    {
        var target = new RenderTargetHandle(7, 42, new Vector2(320f, 200f), 3);

        target.Should().Be(new RenderTargetHandle(7, 42, new Vector2(320f, 200f), 3));
        target.Should().NotBe(new RenderTargetHandle(7, 43, new Vector2(320f, 200f), 3));
        target.Should().NotBe(new RenderTargetHandle(8, 42, new Vector2(320f, 200f), 3));
    }

    [Fact]
    public void RenderTargetHandle_AHandleOfAnEarlierAttachment_IsNotTheTargetThatTookItsNumbers()
    {
        // A renderer that was attached to another window let go of the device of the first one, and the device that is there now
        // hands the same numbers out again: the attachment is what tells a handle of the one from a target of the other.
        var before = new RenderTargetHandle(7, 42, new Vector2(320f, 200f), 1);
        var now = new RenderTargetHandle(7, 42, new Vector2(320f, 200f), 2);

        before.Should().NotBe(now);
    }

    [Fact]
    public void RenderTargetHandle_ADefaultHandle_NamesNoTexture()
    {
        default(RenderTargetHandle).Texture.Id.Should().Be(0, "zero is what a renderer draws as a solid quad");
    }

    [Fact]
    public void IRenderer_ARendererThatDrawsIntoTheWindowOnly_MakesNoTarget()
    {
        IRenderer renderer = new WindowOnlyRenderer();

        Action create = () => renderer.CreateRenderTarget(320, 200);

        create.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void IRenderer_ARendererThatDrawsIntoTheWindowOnly_BindsNoTarget()
    {
        IRenderer renderer = new WindowOnlyRenderer();

        Action begin = () => renderer.BeginRenderTarget(default, clear: true);
        Action end = renderer.EndRenderTarget;

        begin.Should().Throw<NotSupportedException>();
        end.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void IRenderer_ARendererThatDrawsIntoTheWindowOnly_IgnoresARelease()
    {
        IRenderer renderer = new WindowOnlyRenderer();

        Action release = () => renderer.ReleaseRenderTarget(default);

        release.Should().NotThrow("a handle that a renderer did not create is never deleted by number");
    }

    /// <summary>A renderer that keeps the defaults of <see cref="IRenderer"/> for everything a window-only renderer refuses.</summary>
    private sealed class WindowOnlyRenderer : IRenderer
    {
        public Vector2 ViewportSize => new(1280f, 720f);

        public void Attach(IWindowService window) { }

        public void SetCamera(Camera2D camera) { }

        public void BeginFrame(bool clear) { }

        public void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color, float rotation = 0f) { }

        public void DrawTextureRegion(TextureHandle texture, Rect source, Vector2 position, Vector2 size, Color color, float rotation = 0f) { }

        public void DrawRectangle(Rect rect, Color color) { }

        public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color) { }

        public void EndFrame() { }

        public TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height) => new(1);

        public void ReleaseTexture(TextureHandle texture) { }

        public void Dispose() { }
    }
}
