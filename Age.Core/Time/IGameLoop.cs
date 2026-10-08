namespace Age.Core;

/// <summary>
/// Drives the main loop that feeds elapsed time into a tick callback.
/// Blocks the calling thread. TestGameLoop returns after N steps. SilkGameLoop returns after the window closes or Stop is called.
/// </summary>
public interface IGameLoop
{
    /// <summary>Runs the loop, invoking the callback once per frame until the loop stops.</summary>
    /// <remarks>
    /// The callback receives the time that its frame took, so a simulation that adds that time up advances by a
    /// different amount on every frame. Use <c>Run(update, render)</c> for a simulation that advances by a fixed step.
    /// </remarks>
    void Run(Action<GameTime> tick);

    /// <summary>Runs the loop, invoking <paramref name="update"/> for a whole number of fixed steps and <paramref name="render"/> once per frame, after the steps of that frame.</summary>
    /// <param name="update">The simulation. Called zero or more times per frame, each time with a delta of <see cref="FixedTimestep.Step"/>.</param>
    /// <param name="render">The drawing. Called once per frame, with the time that the frame took.</param>
    /// <remarks>
    /// The loop accumulates the frame time in its <see cref="FixedTimestep"/>, so the simulation advances by the same
    /// amount whatever the frame rate is, and a frame that took longer than the maximum frame time is clamped. The
    /// render callback runs after the steps of its own frame, so it draws the world as the simulation left it, and it
    /// runs even when the frame was too short for a step.
    /// </remarks>
    void Run(Action<GameTime> update, Action<GameTime> render);

    /// <summary>Requests that the loop stop after the current iteration.</summary>
    void Stop();
}
