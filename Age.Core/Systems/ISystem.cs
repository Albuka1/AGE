namespace Age.Core;

/// <summary>
/// Defines a unit of behaviour that a <see cref="SystemPipeline"/> runs once per update step, in the order in which it
/// was added to the pipeline.
/// </summary>
/// <remarks>
/// <para>
/// A step carries the time of one <c>GameTime.Delta</c>: a fixed step when the loop was given an update callback,
/// otherwise the time that a frame took. The same system therefore runs a whole number of times per frame. A system
/// that has to follow the display rather than the simulation implements <see cref="IFrameSystem"/> instead: it runs once
/// per frame and keeps running while the clock is paused.
/// </para>
/// <para>
/// A system may be switched off on its own, which is what a developer overlay or a paused menu turns a rule off with:
/// the pipeline asks <see cref="Enabled"/> before it calls the system, so a system that is off costs one property read
/// rather than a call. The whole simulation is stopped by <see cref="FixedTimestep.Paused"/> instead, which is the clock
/// and not the systems.
/// </para>
/// </remarks>
public interface ISystem
{
    /// <summary>Gets or sets a value indicating whether the pipeline runs the system.</summary>
    /// <remarks>A system that is switched off keeps its state and its position in the pipeline: it is skipped for as long as it is off, and nothing is unsubscribed from its events.</remarks>
    bool Enabled
    {
        get => true;
        set
        {
        }
    }

    /// <summary>Advances the system by one step.</summary>
    void Update(World world, in GameTime time);
}
