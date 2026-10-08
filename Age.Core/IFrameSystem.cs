namespace Age.Core;

/// <summary>
/// Defines a unit of behaviour that runs once per frame, with the time the frame took, instead of once per fixed step.
/// </summary>
/// <remarks>
/// A frame system is what follows the display rather than the simulation: a menu that keeps animating, a camera that
/// smooths, a loading spinner, a debug overlay. It keeps running while the simulation stands still, so a paused game
/// still reacts to the pointer and still moves its interface — pausing is <see cref="FixedTimestep.Paused"/>, which
/// stops <see cref="ISystem"/> and leaves the frames alone.
/// </remarks>
/// <example>
/// <code>
/// pipeline.Add(collisions);        // once per step: the simulation
/// pipeline.AddFrame(uiUpdate);     // once per frame: the interface
///
/// gameLoop.Run(
///     update: step => world.Update(step, pipeline),
///     render: frame => world.UpdateFrame(frame, pipeline));
/// </code>
/// </example>
public interface IFrameSystem
{
    /// <summary>Advances the system by one frame.</summary>
    /// <param name="world">The world to read, and to change when the system owns the rule that changes it.</param>
    /// <param name="frame">The time of the frame that just ended. It is not the fixed step, so it differs from frame to frame.</param>
    void UpdateFrame(World world, in GameTime frame);
}
