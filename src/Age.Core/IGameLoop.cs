namespace Age.Core;

/// <summary>
/// Drives the main loop that feeds elapsed time into a tick callback.
/// Blocks the calling thread. TestGameLoop returns after N steps. SilkGameLoop returns after the window closes or Stop is called.
/// </summary>
public interface IGameLoop
{
    /// <summary>Runs the loop, invoking the callback once per frame until the loop stops.</summary>
    void Run(Action<GameTime> tick);

    /// <summary>Requests that the loop stop after the current iteration.</summary>
    void Stop();
}
