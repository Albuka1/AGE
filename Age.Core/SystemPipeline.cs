namespace Age.Core;

/// <summary>
/// Runs an ordered list of systems. A pipeline is a singleton per application and must not be shared across worlds.
/// </summary>
/// <remarks>
/// <para>
/// The systems run in insertion order, one after another, on the thread that calls <see cref="Update"/>, so a system
/// sees the world as the system before it left it.
/// </para>
/// <para>
/// A pipeline holds two lists: the systems of the simulation, which run once per fixed step, and the frame systems,
/// which the game runs once per frame from the render callback of the loop. A long frame runs several of the first and
/// exactly one of the second, and a paused clock runs none of the first while the second keeps going.
/// </para>
/// <para>
/// A <see cref="World"/> is not thread-safe, so its owner calls the pipeline from the update callback of the game loop
/// and serializes whatever touches the same world from another thread.
/// </para>
/// </remarks>
public sealed class SystemPipeline
{
    private readonly List<ISystem> _systems = new();
    private readonly List<IFrameSystem> _frameSystems = new();

    /// <summary>
    /// Appends a system to the pipeline. Not idempotent. Adding the same instance twice causes double execution.
    /// </summary>
    public void Add(ISystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        _systems.Add(system);
    }

    /// <summary>
    /// Appends a system that runs once per frame. Not idempotent. Adding the same instance twice causes double execution.
    /// </summary>
    /// <remarks>
    /// A frame system is not a step system: it is called by <see cref="UpdateFrame"/> from the render callback of the
    /// loop, once per frame, and it keeps running while the clock is paused.
    /// </remarks>
    public void AddFrame(IFrameSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        _frameSystems.Add(system);
    }

    /// <summary>Runs every registered system in insertion order.</summary>
    public void Update(World world, in GameTime time)
    {
        ArgumentNullException.ThrowIfNull(world);

        foreach (ISystem system in _systems)
        {
            system.Update(world, time);
        }
    }

    /// <summary>Runs every registered frame system in insertion order.</summary>
    public void UpdateFrame(World world, in GameTime frame)
    {
        ArgumentNullException.ThrowIfNull(world);

        foreach (IFrameSystem system in _frameSystems)
        {
            system.UpdateFrame(world, frame);
        }
    }
}
