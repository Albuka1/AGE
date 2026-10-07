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
    /// <param name="clear">Requests clearing of the color buffer. The request is honoured only while no earlier call in the frame has cleared it, so an earlier call that passed <see langword="false"/> does not prevent a later clear.</param>
    /// <remarks>
    /// May be called more than once per frame, for example once for the world and once for the UI. The first call that
    /// passes <see langword="true"/> clears to <see cref="Color.Black"/>; every later call leaves the buffer untouched,
    /// even when it also passes <see langword="true"/>. Draws use the camera of the most recent <see cref="SetCamera"/> call.
    /// </remarks>
    void BeginFrame(bool clear);

    /// <summary>Draws a sprite, either textured or as a solid color quad.</summary>
    /// <param name="texture">The texture to sample. A handle whose <see cref="TextureHandle.Id"/> is zero selects a solid color quad.</param>
    /// <param name="position">The top-left corner of the sprite, in world coordinates.</param>
    /// <param name="size">The size of the quad in world coordinates, after the transform scale has been applied. The camera transform, including its zoom, is applied on top of it.</param>
    /// <param name="color">The tint. It is multiplied with the sampled texel, or used as it is for a solid color quad.</param>
    /// <param name="rotation">The rotation of the quad around its centre, in radians. A renderer that ignores it draws the quad upright.</param>
    /// <remarks>
    /// The whole texture is mapped onto the quad, so a rotation turns the image with it. Without a texture the call draws
    /// the same rectangle as <see cref="DrawRectangle"/>, rotated.
    /// </remarks>
    void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color, float rotation = 0f);

    /// <summary>Uploads pixel data as a texture that the renderer owns.</summary>
    /// <param name="pixels">The pixels, four RGBA bytes each, in row-major order from the top-left corner.</param>
    /// <param name="width">The width of the image, in pixels.</param>
    /// <param name="height">The height of the image, in pixels.</param>
    /// <returns>A handle to the texture, for <see cref="DrawSprite"/> and <see cref="ReleaseTexture"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Width or height is zero or negative.</exception>
    /// <exception cref="ArgumentException">The buffer does not hold four bytes for every pixel.</exception>
    /// <exception cref="InvalidOperationException">The renderer has not been attached to a window.</exception>
    TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height);

    /// <summary>Deletes a texture that <see cref="CreateTexture"/> created. A handle without a texture is ignored.</summary>
    /// <param name="texture">The texture to delete.</param>
    void ReleaseTexture(TextureHandle texture);

    /// <summary>Draws a filled rectangle.</summary>
    void DrawRectangle(Rect rect, Color color);

    /// <summary>Draws a single line of text with the built-in bitmap font.</summary>
    void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color);

    /// <summary>Flushes pending draws and ends the frame.</summary>
    void EndFrame();
}
