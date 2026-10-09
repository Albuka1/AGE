using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Draws two-dimensional content through the graphics device.
/// </summary>
/// <remarks>
/// The renderer owns the device objects it creates, so dispose it before the window it was attached to is closed: the
/// shader program, the vertex buffers and the built-in font texture are deleted then, while the context is still alive.
/// Disposing twice, and disposing a renderer that was never attached, both do nothing, and a disposed renderer can be
/// attached to another window, which creates its objects again.
/// </remarks>
public interface IRenderer : IDisposable
{
    /// <summary>Gets the current viewport size, in pixels.</summary>
    Vector2 ViewportSize { get; }

    /// <summary>Gets a number that changes when the renderer is attached to a window, which is what tells the objects of one device from the objects of another.</summary>
    /// <remarks>
    /// <para>
    /// A renderer that is attached to another window lets go of the device of the first one: the programs, the targets and the textures of it are gone, and
    /// the device that is there now hands the same numbers out again. A handle that carries the number of its attachment is therefore refused by the
    /// renderer rather than resolved against whatever holds that number now, and a service that caches a device object builds it again when the number
    /// changed.
    /// </para>
    /// <para>
    /// A renderer that owns no device of its own keeps the number at zero, so a handle of one and the current number always agree.
    /// </para>
    /// </remarks>
    uint DeviceGeneration => 0;

    /// <summary>Binds the renderer to a window and creates its device resources.</summary>
    /// <remarks>
    /// The programs that a game compiled through <see cref="CompileShader"/> are objects of the device as well, so they are
    /// deleted when the renderer lets go of it, and a handle of the window before is refused by <see cref="UseShader"/> rather
    /// than handed to a device that has never seen it. <see cref="DeviceGeneration"/> changes with the attachment, which is what
    /// an <c>IShaderService</c> reads to compile the stages again for the window that is there now.
    /// </remarks>
    void Attach(IWindowService window);

    /// <summary>Sets the camera that is used by subsequent draws.</summary>
    void SetCamera(Camera2D camera);

    /// <summary>Starts a frame and prepares the renderer for draws.</summary>
    /// <param name="clear">Requests clearing of the color buffer. The request is honoured only while no earlier call in the frame has cleared it, so an earlier call that passed <see langword="false"/> does not prevent a later clear.</param>
    /// <remarks>
    /// May be called more than once per frame, for example once for the world and once for the UI. The first call that
    /// passes <see langword="true"/> clears to <see cref="Color.Black"/>; every later call leaves the buffer untouched,
    /// even when it also passes <see langword="true"/>. Draws use the camera of the most recent <see cref="SetCamera"/> call.
    /// A draw that a caller collected outside a frame of its own is drawn here, after the clear of this frame and before what
    /// follows of it, with the camera that was set when it was collected: wrap the draws that belong to one picture in a frame
    /// of their own, because a draw that no frame opened lands in the frame after it and under everything of that one.
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
    /// the same rectangle as <see cref="DrawRectangle"/>, rotated. <see cref="DrawTextureRegion"/> draws a part of a
    /// texture instead, which is what an atlas needs.
    /// </remarks>
    void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color, float rotation = 0f);

    /// <summary>Draws a part of a texture, which is how a sprite sheet or a glyph atlas reaches the screen.</summary>
    /// <param name="texture">The texture to sample. A handle whose <see cref="TextureHandle.Id"/> is zero selects a solid color quad and ignores the region.</param>
    /// <param name="source">The part of the texture that is mapped onto the quad, in normalized coordinates: (0, 0) is the top-left corner of the texture and (1, 1) its bottom-right.</param>
    /// <param name="position">The top-left corner of the quad, in world coordinates.</param>
    /// <param name="size">The size of the quad, in world coordinates.</param>
    /// <param name="color">The tint.</param>
    /// <param name="rotation">The rotation of the quad around its centre, in radians.</param>
    /// <remarks>
    /// <see cref="DrawSprite"/> is this call with the region that covers the whole texture, so a renderer that
    /// implements one of the two implements both through the other.
    /// </remarks>
    void DrawTextureRegion(TextureHandle texture, Rect source, Vector2 position, Vector2 size, Color color, float rotation = 0f);

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

    /// <summary>Creates a surface that draws go into instead of the window, with a texture that holds what was drawn.</summary>
    /// <param name="width">The width of the target, in pixels.</param>
    /// <param name="height">The height of the target, in pixels.</param>
    /// <returns>The handle of the target, for <see cref="BeginRenderTarget"/>, <see cref="EndRenderTarget"/> and <see cref="ReleaseRenderTarget"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Width or height is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">The renderer has not been attached to a window, or the device refused a target of that size.</exception>
    /// <remarks>
    /// A renderer that draws into the window only refuses this. A target is what a frame of several layers is built from: a
    /// pass draws into one, the result of it is a texture that another pass samples, and the last pass draws into the window.
    /// </remarks>
    RenderTargetHandle CreateRenderTarget(int width, int height) =>
        throw new NotSupportedException("This renderer draws into the window only.");

    /// <summary>Points the following draws at a target instead of the window.</summary>
    /// <param name="target">The target to draw into.</param>
    /// <param name="clear">Requests clearing of the color buffer of the target. The request is honoured only while that surface has not been cleared in this frame.</param>
    /// <exception cref="ArgumentException">The handle is not a target that this renderer created, or it belongs to a device the renderer is no longer attached to.</exception>
    /// <remarks>
    /// Everything drawn until <see cref="EndRenderTarget"/> goes into the texture of the target, and
    /// <see cref="ViewportSize"/> answers with the size of the target, so a pass, a camera and a layout that measure what
    /// they draw measure the target rather than the window. A pass that draws into a target is a pass like any other: it
    /// sets its camera and opens its frame after this call. A surface is cleared at most once in a frame, which is what
    /// makes the clear of the window survive a round trip through a target.
    /// </remarks>
    void BeginRenderTarget(RenderTargetHandle target, bool clear) =>
        throw new NotSupportedException("This renderer draws into the window only.");

    /// <summary>Points the following draws at the window again, and measures it in the size of the window.</summary>
    /// <remarks>
    /// The window is read again here, because the frame that follows can be the first one after a resize. What was drawn
    /// into the target stays in the texture of its handle, which a game binds as a sampler or draws as a quad.
    /// </remarks>
    void EndRenderTarget() =>
        throw new NotSupportedException("This renderer draws into the window only.");

    /// <summary>Deletes a target that <see cref="CreateRenderTarget"/> created, together with the texture of it.</summary>
    /// <param name="target">The target to delete.</param>
    /// <remarks>
    /// A target that is still bound is left first, so what follows draws into the window again, and a handle that this
    /// renderer did not create is ignored rather than deleted by number.
    /// </remarks>
    void ReleaseRenderTarget(RenderTargetHandle target)
    {
    }

    /// <summary>Draws a filled rectangle.</summary>
    void DrawRectangle(Rect rect, Color color);

    /// <summary>Draws a single line of text with the built-in bitmap font.</summary>
    void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color);

    /// <summary>Compiles a program from the two stages of a shader.</summary>
    /// <param name="vertexSource">The vertex stage, in the OpenGL Shading Language.</param>
    /// <param name="fragmentSource">The fragment stage, in the OpenGL Shading Language.</param>
    /// <returns>The identifier of the program, for <see cref="UseShader"/> and <see cref="ReleaseShader(uint)"/>.</returns>
    /// <exception cref="InvalidOperationException">The renderer has not been attached to a window, or a stage does not compile.</exception>
    /// <remarks>
    /// A renderer that draws without shaders of its own refuses this. A game does not compile a shader itself: it asks
    /// <c>IShaderService</c>, which reads the stages of the content and caches the program.
    /// </remarks>
    uint CompileShader(string vertexSource, string fragmentSource) =>
        throw new NotSupportedException("This renderer draws without shaders of their own.");

    /// <summary>Deletes a program that <see cref="CompileShader"/> created. A program of zero is ignored.</summary>
    /// <param name="program">The identifier of the program.</param>
    void ReleaseShader(uint program)
    {
    }

    /// <summary>Deletes a program that <see cref="CompileShader"/> created, through the handle of the shader it belongs to.</summary>
    /// <param name="shader">The handle of the shader to delete.</param>
    /// <remarks>
    /// A handle that names another attachment than <see cref="DeviceGeneration"/> is ignored rather than deleted by number, because the program it names
    /// went away with the device that compiled it and the number of it belongs to whatever the device that is there now gave it to. A renderer that draws
    /// without shaders of its own ignores this, as it ignores <see cref="ReleaseShader(uint)"/>.
    /// </remarks>
    void ReleaseShader(ShaderHandle shader) => ReleaseShader(shader.Program);

    /// <summary>Draws every quad after this call with a shader.</summary>
    /// <param name="shader">The shader to draw with, which the renderer drew no quad of yet or drew quads of already.</param>
    /// <remarks>
    /// What a caller collected before this call is drawn first, because one draw call samples one program: the quads that are
    /// gathered under the shader that is being replaced are a batch of their own. A renderer that draws without shaders of its
    /// own refuses this, and so does a handle that belongs to a device the renderer is no longer attached to, which is a handle
    /// whose <see cref="ShaderHandle.Generation"/> is not <see cref="DeviceGeneration"/>.
    /// </remarks>
    void UseShader(ShaderHandle shader) =>
        throw new NotSupportedException("This renderer draws without shaders of their own.");

    /// <summary>Draws every quad after this call with the shader the renderer was created with.</summary>
    /// <remarks>What a caller collected under the shader that is being replaced is drawn first, as with <see cref="UseShader"/>.</remarks>
    void ResetShader()
    {
    }

    /// <summary>Sets a uniform of the shader that is being used to a number.</summary>
    /// <param name="name">The name of the uniform, as the shader declares it.</param>
    /// <param name="value">The value to set.</param>
    /// <remarks>What a caller collected before this call is drawn first, because a uniform is one value for a whole draw call.</remarks>
    void SetUniform(string name, float value) => throw new NotSupportedException("This renderer draws without shaders of their own.");

    /// <summary>Sets a uniform of the shader that is being used to a whole number.</summary>
    /// <param name="name">The name of the uniform, as the shader declares it.</param>
    /// <param name="value">The value to set.</param>
    void SetUniform(string name, int value) => throw new NotSupportedException("This renderer draws without shaders of their own.");

    /// <summary>Sets a uniform of the shader that is being used to a vector of two, three or four numbers.</summary>
    /// <param name="name">The name of the uniform, as the shader declares it.</param>
    /// <param name="values">The values of the vector, which decides its width.</param>
    /// <exception cref="ArgumentException"><paramref name="values"/> holds fewer than two or more than four numbers.</exception>
    void SetUniform(string name, ReadOnlySpan<float> values) => throw new NotSupportedException("This renderer draws without shaders of their own.");

    /// <summary>Binds a texture to a sampler of the shader that is being used.</summary>
    /// <param name="name">The name of the sampler uniform, as the shader declares it.</param>
    /// <param name="texture">The texture to sample. A handle of zero binds no image.</param>
    /// <param name="unit">The texture unit to bind it to, which the sampler is told to read.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="unit"/> is below two, because the first unit is where the engine binds the image that is being drawn and the second one is where it binds the surface that a stage reads as <c>SCREEN_TEXTURE</c>.</exception>
    void SetSampler(string name, TextureHandle texture, int unit) => throw new NotSupportedException("This renderer draws without shaders of their own.");

    /// <summary>Flushes pending draws and ends the frame.</summary>
    void EndFrame();
}
