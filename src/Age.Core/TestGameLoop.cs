namespace Age.Core;

/// <summary>
/// A deterministic game loop that runs a fixed number of steps. It is intended for tests.
/// </summary>
public sealed class TestGameLoop : IGameLoop
{
    private readonly int _steps;
    private readonly float _deltaSeconds;
    private bool _stopRequested;

    /// <summary>Initializes a loop that runs a fixed number of steps of a fixed duration.</summary>
    public TestGameLoop(int steps, float deltaSeconds)
    {
        _steps = steps;
        _deltaSeconds = deltaSeconds;
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

    /// <summary>Requests that the loop stop before the next step.</summary>
    public void Stop() => _stopRequested = true;
}
