using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Runs an ordered list of render passes. A pipeline is a singleton per application and must not be shared across windows.
/// </summary>
/// <remarks>
/// The order of the list is the order of the frame: add the world, then the UI, then what a game adds of its own, such
/// as post-processing. The pipeline is registered empty by <c>AddAgeRendering</c>, so a game decides what its frames are
/// made of. Registering the passes there instead of calling them after <see cref="World.Update"/> keeps the order in
/// one place, which is what the render callback of the game loop runs.
/// </remarks>
/// <example>
/// <code>
/// renderPipeline.Add(renderSystem);
/// renderPipeline.Add(uiRenderSystem);
///
/// gameLoop.Run(
///     update: step =&gt; world.Update(step, pipeline),
///     render: time =&gt; renderPipeline.Render(world, camera));
/// </code>
/// </example>
public sealed class RenderPipeline
{
    private readonly List<IRenderPass> _passes = new();

    /// <summary>Gets the number of passes in the pipeline.</summary>
    public int Count => _passes.Count;

    /// <summary>Appends a pass to the pipeline. Not idempotent. Adding the same instance twice draws it twice.</summary>
    /// <param name="pass">The pass to append.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pass"/> is null.</exception>
    public void Add(IRenderPass pass)
    {
        ArgumentNullException.ThrowIfNull(pass);
        _passes.Add(pass);
    }

    /// <summary>Runs every pass in insertion order.</summary>
    /// <param name="world">The world to draw.</param>
    /// <param name="camera">The camera of the frame.</param>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is null.</exception>
    public void Render(World world, in Camera2D camera)
    {
        ArgumentNullException.ThrowIfNull(world);

        foreach (IRenderPass pass in _passes)
        {
            pass.Render(world, camera);
        }
    }
}
