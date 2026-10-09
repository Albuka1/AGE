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
/// A renderer that was attached to another window compiles against another device, so the handles of the window before are
/// refused: <see cref="UnloadAll"/> forgets them without touching a device that is gone, and the shaders are loaded again,
/// which compiles them for the window that is there now.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// IShaderService shaders = provider.GetRequiredService&lt;IShaderService&gt;();
/// ShaderHandle displacement = shaders.Load("Shaders/displacement.frag");
///
/// renderer.UseShader(displacement);
/// renderer.SetUniform("uDisplacementSize", 4f);
/// renderer.SetSampler("uDisplacement", textures.Resolve("Textures/Effects/height.png"), 1);
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
    /// <remarks>The stages are compiled and linked once, so loading the same pair of paths twice returns the same shader.</remarks>
    ShaderHandle Load(string fragmentPath, string? vertexPath = null);

    /// <summary>Determines whether the handle still refers to a shader that this service compiled.</summary>
    /// <param name="shader">The handle to check.</param>
    /// <returns><see langword="true"/> while the shader is held. A handle from before an unload returns <see langword="false"/>.</returns>
    bool IsAlive(ShaderHandle shader);

    /// <summary>Deletes the program behind the handle and forgets its paths, so loading them again compiles it anew.</summary>
    /// <param name="shader">The handle of the shader to delete.</param>
    /// <returns><see langword="true"/> when a shader was deleted, <see langword="false"/> when the handle was stale or not owned by this service.</returns>
    bool Unload(ShaderHandle shader);

    /// <summary>Deletes every shader that this service compiled.</summary>
    void UnloadAll();
}
