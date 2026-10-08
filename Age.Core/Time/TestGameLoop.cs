namespace Age.Core;

/// <summary>
/// A deterministic game loop that runs a fixed number of steps. It is intended for tests.
/// </summary>
public sealed class TestGameLoop : IGameLoop
{
    private readonly int _steps;
    private readonly float _deltaSeconds;
    private readonly FixedTimestep _timestep;
    private bool _stopRequested;

    /// <summary>Initializes a loop that runs a fixed number of steps of a fixed duration, with the default fixed step.</summary>
    /// <param name="steps">The number of frames to run.</param>
    /// <param name="deltaSeconds">The time each frame took, in seconds.</param>
    public TestGameLoop(int steps, float deltaSeconds)
        : this(steps, deltaSeconds, new FixedTimestep(FixedTimestep.DefaultStep))
    {
    }

    /// <summary>Initializes a loop that runs a fixed number of steps of a fixed duration.</summary>
    /// <param name="steps">The number of frames to run.</param>
    /// <param name="deltaSeconds">The time each frame took, in seconds. <c>Run(update, render)</c> accumulates it and advances by <paramref name="timestep"/>.</param>
    /// <param name="timestep">The fixed step that the two-callback overload advances with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timestep"/> is null.</exception>
    public TestGameLoop(int steps, float deltaSeconds, FixedTimestep timestep)
    {
        ArgumentNullException.ThrowIfNull(timestep);
        _steps = steps;
        _deltaSeconds = deltaSeconds;
        _timestep = timestep;
    }

    /// <summary>
    /// Invokes the tick callback exactly the configured number of times. If Stop was called before Run, Run returns immediately without calling tick.
    /// </summary>
    public void Run(Action<GameTime> tick)
    {
        ArgumentNullException.ThrowIfNull(tick);

        for (int step = 0; step < _steps; step++)
        {
            if (_stopRequested)
            {
                return;
            }

            tick(new GameTime(_deltaSeconds, (step + 1) * _deltaSeconds));
        }
    }

    /// <summary>
    /// Runs the configured number of frames and advances the simulation by the fixed step of this loop. A frame that is
    /// shorter than the step reports no update at all, and one that is longer reports several.
    /// </summary>
    public void Run(Action<GameTime> update, Action<GameTime> render)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(render);

        for (int step = 0; step < _steps; step++)
        {
            if (_stopRequested)
            {
                return;
            }

            var frame = new GameTime(_deltaSeconds, (step + 1) * _deltaSeconds);
            _timestep.Advance(frame, update);
            render(frame);
        }
    }

    /// <summary>Requests that the loop stop before the next step.</summary>
    public void Stop() => _stopRequested = true;
}
