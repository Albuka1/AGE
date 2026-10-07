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
/// A <see cref="World"/> is not thread-safe, so its owner calls the pipeline from the update callback of the game loop
/// and serializes whatever touches the same world from another thread.
/// </para>
/// </remarks>
public sealed class SystemPipeline
{
    private readonly List<ISystem> _systems = new();

    /// <summary>
    /// Appends a system to the pipeline. Not idempotent. Adding the same instance twice causes double execution.
    /// </summary>
    public void Add(ISystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        _systems.Add(system);
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
}
