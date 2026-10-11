using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

/// <summary>
/// Pins the contract of the clip to a caller: the defaults of <see cref="IRenderer"/> for a renderer that draws without
/// clipping, and the nesting rule that a recorder of the calls can hold without a device.
/// </summary>
public sealed class ClipTests
{
    [Fact]
    public void IRenderer_ARendererThatDrawsWithoutClipping_IgnoresAPushAndAPop()
    {
        IRenderer renderer = new WindowOnlyRenderer();

        Action push = () => renderer.PushClip(new Rect(Vector2.Zero, new Vector2(10f, 10f)));
        Action pop = renderer.PopClip;

        push.Should().NotThrow("a renderer without clipping keeps the default that does nothing");
        pop.Should().NotThrow();
    }

    [Fact]
    public void IRenderer_PoppingPastTheOutermostClip_DoesNothing()
    {
        var renderer = new RecordingRenderer();

        Action pop = renderer.PopClip;

        pop.Should().NotThrow("a pop that a renderer was not told about is the safest thing to lose");
        renderer.Clips.Should().BeEmpty();
    }

    [Fact]
    public void IRenderer_APushInsideAClip_KeepsTheSharedPart()
    {
        var renderer = new RecordingRenderer();

        renderer.PushClip(new Rect(Vector2.Zero, new Vector2(100f, 80f)));
        renderer.PushClip(new Rect(new Vector2(40f, 30f), new Vector2(100f, 80f)));

        renderer.Clips.Should().HaveCount(2);
        renderer.Clips[^1].Should().Be(new Rect(new Vector2(40f, 30f), new Vector2(60f, 50f)), "a push nests, so what is in force is the intersection with the clip below it");

        renderer.PopClip();

        renderer.Clips.Should().ContainSingle().Which.Should().Be(new Rect(Vector2.Zero, new Vector2(100f, 80f)));
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

    /// <summary>A renderer that records the clip stack the way the scissor of a device would end up.</summary>
    private sealed class RecordingRenderer : IRenderer
    {
        public List<Rect> Clips { get; } = [];

        public Vector2 ViewportSize => new(1280f, 720f);

        public void Attach(IWindowService window) { }

        public void SetCamera(Camera2D camera) { }

        public void BeginFrame(bool clear) { }

        public void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color, float rotation = 0f) { }

        public void DrawTextureRegion(TextureHandle texture, Rect source, Vector2 position, Vector2 size, Color color, float rotation = 0f) { }

        public void DrawRectangle(Rect rect, Color color) { }

        public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color) { }

        public void PushClip(Rect rect) => Clips.Add(Clips.Count == 0 ? rect : Clips[^1].Intersect(rect));

        public void PopClip()
        {
            if (Clips.Count > 0)
            {
                Clips.RemoveAt(Clips.Count - 1);
            }
        }

        public void EndFrame() { }

        public TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height) => new(1);

        public void ReleaseTexture(TextureHandle texture) { }

        public void Dispose() { }
    }
}
