using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Draws two-dimensional content through the graphics device.
/// </summary>
public interface IRenderer
{
    /// <summary>Gets the current viewport size, in pixels.</summary>
    Vector2 ViewportSize { get; }

    /// <summary>Binds the renderer to a window and creates its device resources.</summary>
    void Attach(IWindowService window);

    /// <summary>Sets the camera that is used by subsequent draws.</summary>
    void SetCamera(Camera2D camera);

    /// <summary>
    /// May be called multiple times per frame. Only the first call with clear=true performs GL.Clear.
    /// Clears to Color.Black. Subsequent BeginFrame(false) calls do not clear. The camera is applied from the last SetCamera call.
    /// </summary>
    void BeginFrame(bool clear);

    /// <summary>
    /// Draws a sprite. position is the top-left corner of the sprite in world coordinates. size is the final pixel size on screen.
    /// If texture.Id is zero, renders a solid color quad equivalent to DrawRectangle(new Rect(position, size), color).
    /// </summary>
    void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color);

    /// <summary>Draws a filled rectangle.</summary>
    void DrawRectangle(Rect rect, Color color);

    /// <summary>Draws a single line of text with the built-in bitmap font.</summary>
    void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color);

    /// <summary>Flushes pending draws and ends the frame.</summary>
    void EndFrame();
}
