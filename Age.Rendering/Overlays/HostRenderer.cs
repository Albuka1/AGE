using Age.Core;

namespace Age.Rendering;

/// <summary>
/// An <see cref="IRenderer"/> that draws a page of a tab into an <see cref="IDevWindowHost"/>, which is what lets a page written
/// against the renderer of the engine draw itself in the window of the operating system without knowing it is there.
/// </summary>
/// <remarks>
/// A page of a tab draws rectangles and text, and this answers those two and nothing else: sprites, textures and shaders belong to
/// the window of the game, whose context is not the one this draws into. A page that reaches for one of them is a page that belongs
/// in the game rather than in a window beside it, and the call it makes says so.
/// </remarks>
internal sealed class HostRenderer(IDevWindowHost host) : IRenderer
{
    /// <inheritdoc />
    /// <remarks>The host reports the size of its own window, which is what a page is laid out in.</remarks>
    public Vector2 ViewportSize => host.Size;

    /// <inheritdoc />
    public void BeginFrame(bool clear) => host.BeginFrame(clear);

    /// <inheritdoc />
    public void EndFrame() => host.EndFrame();

    /// <inheritdoc />
    public void DrawRectangle(Rect rect, Color color) => host.DrawRectangle(rect, color);

    /// <inheritdoc />
    public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color) => host.DrawText(text.ToString(), position, color);

    /// <inheritdoc />
    public void Attach(IWindowService window) => throw new NotSupportedException("A window host is attached to the window it was made for.");

    /// <inheritdoc />
    public void SetCamera(Camera2D camera) => throw new NotSupportedException("A window beside the game draws in its own pixels rather than through a camera of the world.");

    /// <inheritdoc />
    public void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color, float rotation = 0f) => throw new NotSupportedException("A window beside the game draws no sprite of the world.");

    /// <inheritdoc />
    public void DrawTextureRegion(TextureHandle texture, Rect source, Vector2 position, Vector2 size, Color color, float rotation = 0f) => throw new NotSupportedException("A window beside the game draws no texture of the world.");

    /// <inheritdoc />
    public TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height) => throw new NotSupportedException("A window beside the game owns no textures of the world.");

    /// <inheritdoc />
    public void ReleaseTexture(TextureHandle texture) => throw new NotSupportedException("A window beside the game owns no textures of the world.");

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
