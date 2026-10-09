using System.Runtime.ExceptionServices;
using Age.Assets;
using Age.Core;

namespace Age.Rendering;

/// <summary>
/// The default <see cref="IShaderService"/>: it reads the stages of a shader through <see cref="IAssetLoader"/> and compiles them
/// with the renderer, one program per pair of stages.
/// </summary>
/// <remarks>
/// A shader is compiled once and its program is cached under the pair of paths it was compiled from, which is what a tuple key
/// is for: loading the same pair of stages twice draws with one program. The service owns the programs it compiled, so
/// disposing it releases them all, and a program that a renderer refuses to release stays held, so another call retries it.
/// </remarks>
public sealed class ShaderService : IShaderService, IDisposable
{
    private readonly IAssetLoader _assets;
    private readonly IRenderer _renderer;
    private readonly ResourcePool<(string Fragment, string Vertex), uint> _programs = new();
    private bool _disposed;

    /// <summary>Initializes the service with the loader of the stages and the renderer that compiles them.</summary>
    /// <param name="assets">The loader that reads the sources.</param>
    /// <param name="renderer">The renderer that compiles them into a program.</param>
    /// <exception cref="ArgumentNullException">The loader or the renderer is null.</exception>
    public ShaderService(IAssetLoader assets, IRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(renderer);
        _assets = assets;
        _renderer = renderer;
    }

    /// <inheritdoc />
    public int Count => _programs.Count;

    /// <inheritdoc />
    public ShaderHandle Load(string fragmentPath, string? vertexPath = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(fragmentPath);

        var key = (Fragment: fragmentPath, Vertex: vertexPath ?? string.Empty);

        if (_programs.TryGetHandle(key, out ResourceHandle slot) && _programs.TryGet(slot, out uint cached))
        {
            return new ShaderHandle(slot, cached);
        }

        // The stages are compiled from what a game wrote, under the header of the engine, and a stage that a game did not write
        // is the one of the engine, which places the quad of the renderer.
        uint program = _renderer.CompileShader(
            ShaderSource.Vertex(vertexPath is null ? null : _assets.Load<string>(vertexPath)),
            ShaderSource.Fragment(_assets.Load<string>(fragmentPath)));

        try
        {
            slot = _programs.Add(program, key);
        }
        catch
        {
            // The program is registered nowhere, so delete it here instead of leaking it for the life of the game.
            _renderer.ReleaseShader(program);
            throw;
        }

        return new ShaderHandle(slot, program);
    }

    /// <inheritdoc />
    public bool IsAlive(ShaderHandle shader) => shader.Resource.IsValid && _programs.TryGet(shader.Resource, out _);

    /// <inheritdoc />
    public bool Unload(ShaderHandle shader)
    {
        if (!_programs.TryGet(shader.Resource, out uint program))
        {
            return false;
        }

        _renderer.ReleaseShader(program);
        _programs.Release(shader.Resource);
        return true;
    }

    /// <inheritdoc />
    public void UnloadAll()
    {
        ExceptionDispatchInfo? failure = null;

        // Release one program at a time and forget a slot only once its release succeeded, so a renderer that refuses one
        // program leaves it held for a later attempt and every other program still unloads in this call.
        foreach (ResourceHandle slot in _programs.GetHandles())
        {
            if (!_programs.TryGet(slot, out uint program))
            {
                continue;
            }

            try
            {
                _renderer.ReleaseShader(program);
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
                continue;
            }

            _programs.Release(slot);
        }

        failure?.Throw();
    }

    /// <summary>Releases every program and marks the service as disposed, so it cannot load another shader.</summary>
    /// <remarks>The call is idempotent, and <see cref="UnloadAll"/> stays available afterwards, which is what retries a program that the renderer refused to release.</remarks>
    public void Dispose()
    {
        _disposed = true;
        UnloadAll();
    }
}
