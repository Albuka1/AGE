namespace Age.Rendering;

/// <summary>
/// Compiles the shaders of a game from the content of a game and keeps one program per pair of stages.
/// </summary>
/// <remarks>
/// <para>
/// A shader is content: a game writes it next to its images and sounds, names it by path, and the engine compiles it and caches
/// the program it became, so two draws that name the same shader share one program. A path is read through
/// <see cref="T:Age.Assets.IAssetLoader"/>, so a shader that a build does not ship, and a shader that the device refuses to
/// compile, both say so when a game loads them rather than at the first frame that draws with them.
/// </para>
/// <para>
/// A shader is a fragment stage of its own and, when it needs it, a vertex stage: a game that names the fragment stage only is
/// drawn with the vertex stage of the engine, which places the quad of the renderer.
/// </para>
/// <para>
/// A stage is written under the header of the engine, which names the surface that is being drawn into as
/// <c>SCREEN_TEXTURE</c>, its size in pixels as <c>SCREEN_SIZE</c> and a reader of it as <c>sampleScreen</c>: the engine copies
/// that surface into a texture for a program that declares the sampler, before the first quad of it is drawn, so a shader of a
/// game draws a picture of the frame over the frame — the world, the text and the interface as they stand when the pass of the
/// shader runs.
/// </para>
/// <para>
/// A renderer that was attached to another window compiles against another device, so a program of the window before is gone
/// and a handle of it is refused by <see cref="IRenderer.UseShader"/>. Loading a shader whose program belongs to that earlier
/// attachment compiles the stages again for the device that is there now, and <see cref="UnloadAll"/> forgets such a program
/// without touching the device of the attachment that is gone.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// IShaderService shaders = provider.GetRequiredService&lt;IShaderService&gt;();
/// ShaderHandle displacement = shaders.Load("Shaders/displacement.frag");
///
/// renderer.UseShader(displacement);
/// renderer.SetUniform("uDisplacementSize", 4f);
/// renderer.SetSampler("uDisplacement", textures.Resolve("Textures/Effects/height.png"), 2);
/// </code>
/// </example>
public interface IShaderService
{
    /// <summary>Gets the number of shaders that this service compiled and still holds.</summary>
    int Count { get; }

    /// <summary>Returns the shader of a fragment stage, compiling it on the first call for that pair of stages.</summary>
    /// <param name="fragmentPath">The path of the fragment stage, relative to the game root.</param>
    /// <param name="vertexPath">The path of the vertex stage, relative to the game root, or null for the stage of the engine.</param>
    /// <returns>The handle of the compiled shader.</returns>
    /// <exception cref="ArgumentException">A path is null, empty or whitespace.</exception>
    /// <exception cref="FileNotFoundException">No file exists at a path.</exception>
    /// <exception cref="InvalidOperationException">The renderer has not been attached to a window, or the device refused to compile a stage.</exception>
    /// <remarks>The stages are compiled and linked once, so loading the same pair of paths twice returns the same shader. A renderer that was attached to another window since is another device, so a shader that was compiled for the window before is compiled again rather than resolved to the program of a device that is gone.</remarks>
    ShaderHandle Load(string fragmentPath, string? vertexPath = null);

    /// <summary>Determines whether the handle still refers to a shader that this service compiled.</summary>
    /// <param name="shader">The handle to check.</param>
    /// <returns><see langword="true"/> while the shader is held. A handle from before an unload returns <see langword="false"/>.</returns>
    bool IsAlive(ShaderHandle shader);

    /// <summary>Deletes the program behind the handle and forgets its paths, so loading them again compiles it anew.</summary>
    /// <param name="shader">The handle of the shader to delete.</param>
    /// <returns><see langword="true"/> when the service still held the shader, <see langword="false"/> when the handle was stale or not owned by this service.</returns>
    /// <remarks>A shader whose program was compiled for a window that the renderer was attached to before is forgotten without a call to the device, because the program went away with the device of that window.</remarks>
    bool Unload(ShaderHandle shader);

    /// <summary>Deletes every shader that this service compiled.</summary>
    void UnloadAll();
}
