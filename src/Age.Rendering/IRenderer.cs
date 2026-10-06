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

    /// <summary>Starts a frame and prepares the renderer for draws.</summary>
    /// <param name="clear">Requests clearing of the color buffer. Only the first call of a frame honours it.</param>
    /// <remarks>
    /// May be called more than once per frame, for example once for the world and once for the UI. The first call that
    /// passes <see langword="true"/> clears to <see cref="Color.Black"/>; every later call leaves the buffer untouched,
    /// even when it also passes <see langword="true"/>. Draws use the camera of the most recent <see cref="SetCamera"/> call.
    /// </remarks>
    void BeginFrame(bool clear);

    /// <summary>Draws a sprite, either textured or as a solid color quad.</summary>
    /// <param name="texture">The texture to sample. A handle whose <see cref="TextureHandle.Id"/> is zero selects a solid color quad.</param>
    /// <param name="position">The top-left corner of the sprite, in world coordinates.</param>
    /// <param name="size">The final size on screen, in pixels, after the transform scale was applied.</param>
    /// <param name="color">The tint. It is multiplied with the sampled texel, or used as it is for a solid color quad.</param>
    /// <remarks>The whole texture is mapped onto the quad. Without a texture the call draws the same rectangle as <see cref="DrawRectangle"/>.</remarks>
    void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color);

    /// <summary>Draws a filled rectangle.</summary>
    void DrawRectangle(Rect rect, Color color);

    /// <summary>Draws a single line of text with the built-in bitmap font.</summary>
    void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color);

    /// <summary>Flushes pending draws and ends the frame.</summary>
    void EndFrame();
}
