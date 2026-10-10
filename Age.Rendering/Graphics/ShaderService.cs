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
/// A program is an object of the device that compiled it, so an entry keeps the attachment of the renderer it was compiled
/// under as well: a renderer that was attached to another window changed that number, and loading the same paths again
/// compiles the stages for the device that is there now rather than handing out the program of a device that is gone.
/// </remarks>
public sealed class ShaderService : IShaderService, IDisposable
{
    private readonly IAssetLoader _assets;
    private readonly IRenderer _renderer;
    private readonly ResourcePool<(string Fragment, string Vertex), Program> _programs = new();
    private bool _disposed;

    /// <summary>The program of one pair of stages, together with the attachment of the renderer that compiled it.</summary>
    /// <param name="Id">The identifier of the program of the device.</param>
    /// <param name="Generation">The attachment of the renderer that compiled it, as <see cref="IRenderer.DeviceGeneration"/> reported it.</param>
    private readonly record struct Program(uint Id, uint Generation);

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
        uint generation = _renderer.DeviceGeneration;

        if (_programs.TryGetHandle(key, out ResourceHandle slot) && _programs.TryGet(slot, out Program cached))
        {
            if (cached.Generation == generation)
            {
                return new ShaderHandle(slot, cached.Id, cached.Generation);
            }

            // A renderer that was attached to another window compiles against another device, so the program of this entry was
            // deleted with the device of the attachment before, and the device that is there now hands those numbers out
            // again: the entry is forgotten without touching the device, and the stages are compiled for the window that is
            // there now.
            _programs.Release(slot);
        }

        // The stages are compiled from what a game wrote, under the header of the engine, and a stage that a game did not write
        // is the one of the engine, which places the quad of the renderer.
        uint program = _renderer.CompileShader(
            ShaderSource.Vertex(vertexPath is null ? null : _assets.Load<string>(vertexPath)),
            ShaderSource.Fragment(_assets.Load<string>(fragmentPath)));

        try
        {
            slot = _programs.Add(new Program(program, generation), key);
        }
        catch
        {
            // The program is registered nowhere, so delete it here instead of leaking it for the life of the game.
            _renderer.ReleaseShader(program);
            throw;
        }

        return new ShaderHandle(slot, program, generation);
    }

    /// <inheritdoc />
    public bool IsAlive(ShaderHandle shader) =>
        shader.Resource.IsValid && shader.Generation == _renderer.DeviceGeneration && _programs.TryGet(shader.Resource, out _);

    /// <inheritdoc />
    public bool Unload(ShaderHandle shader)
    {
        if (!_programs.TryGet(shader.Resource, out Program program))
        {
            return false;
        }

        ReleaseProgram(shader.Resource, program);
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
            if (!_programs.TryGet(slot, out Program program))
            {
                continue;
            }

            try
            {
                ReleaseProgram(slot, program);
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

    /// <summary>Releases the program of an entry, unless it belongs to an attachment that the renderer left.</summary>
    /// <param name="slot">The slot that the entry occupies.</param>
    /// <param name="program">The program of the entry and the attachment it was compiled under.</param>
    /// <remarks>
    /// A device that is let go of deletes the programs that were compiled against it, and the device that is there now hands
    /// the same numbers out again: releasing a program of an attachment that the renderer left by its number would delete
    /// whatever the device gave that number to. Such a program is forgotten without a call, which is what the caller of this
    /// method does next, and <see cref="IRenderer.ReleaseShader(ShaderHandle)"/> refuses the same handle for the same reason.
    /// </remarks>
    private void ReleaseProgram(ResourceHandle slot, Program program)
    {
        if (program.Generation == _renderer.DeviceGeneration)
        {
            _renderer.ReleaseShader(new ShaderHandle(slot, program.Id, program.Generation));
        }
    }

    /// <summary>Releases every program and marks the service as disposed, so it cannot load another shader.</summary>
    /// <remarks>The call is idempotent, and <see cref="UnloadAll"/> stays available afterwards, which is what retries a program that the renderer refused to release.</remarks>
    public void Dispose()
    {
        _disposed = true;
        UnloadAll();
    }
}
