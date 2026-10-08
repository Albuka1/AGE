using System.Diagnostics;

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
    private readonly List<SystemTiming> _stepTimings = new();
    private readonly List<SystemTiming> _frameTimings = new();

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
    /// <remarks>The time every system spent is recorded in <see cref="StepTimings"/>, which a developer overlay reads.</remarks>
    public void Update(World world, in GameTime time)
    {
        ArgumentNullException.ThrowIfNull(world);

        _stepTimings.Clear();

        foreach (ISystem system in _systems)
        {
            long started = Stopwatch.GetTimestamp();

            try
            {
                system.Update(world, time);
            }
            finally
            {
                // The time is recorded even when the system throws: a system that failed still spent time, and the
                // overlay of a developer is exactly where that has to be visible.
                _stepTimings.Add(new SystemTiming(system.GetType().Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds));
            }
        }
    }

    /// <summary>Runs every registered frame system in insertion order.</summary>
    /// <remarks>The time every system spent is recorded in <see cref="FrameTimings"/>, which a developer overlay reads.</remarks>
    public void UpdateFrame(World world, in GameTime frame)
    {
        ArgumentNullException.ThrowIfNull(world);

        _frameTimings.Clear();

        foreach (IFrameSystem system in _frameSystems)
        {
            long started = Stopwatch.GetTimestamp();

            try
            {
                system.UpdateFrame(world, frame);
            }
            finally
            {
                _frameTimings.Add(new SystemTiming(system.GetType().Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds));
            }
        }
    }

    /// <summary>Gets the time every step system spent inside the last step, in milliseconds, in the order the systems ran.</summary>
    public IReadOnlyList<SystemTiming> StepTimings => _stepTimings;

    /// <summary>Gets the time every frame system spent inside the last frame, in milliseconds, in the order the systems ran.</summary>
    public IReadOnlyList<SystemTiming> FrameTimings => _frameTimings;
}
