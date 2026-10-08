namespace Age.Core;

/// <summary>
/// Defines a unit of behaviour that a <see cref="SystemPipeline"/> runs once per update step, in the order in which it
/// was added to the pipeline.
/// </summary>
/// <remarks>
/// A step carries the time of one <c>GameTime.Delta</c>: a fixed step when the loop was given an update callback,
/// otherwise the time that a frame took. The same system therefore runs a whole number of times per frame. A system
/// that has to follow the display rather than the simulation implements <see cref="IFrameSystem"/> instead: it runs once
/// per frame and keeps running while the clock is paused.
/// </remarks>
public interface ISystem
{
    /// <summary>Advances the system by one step.</summary>
    void Update(World world, in GameTime time);
}
