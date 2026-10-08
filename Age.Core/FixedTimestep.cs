namespace Age.Core;

/// <summary>
/// Turns the time of a frame into a whole number of fixed steps, so a simulation advances by the same amount whatever
/// the frame rate is.
/// </summary>
/// <remarks>
/// <para>
/// A loop reports how long each frame took and this type accumulates it. <see cref="Advance"/> hands the callback as
/// many steps as the accumulated time covers, each of them <see cref="Step"/> long, and keeps the rest for the next
/// frame. A frame that took longer than <see cref="MaxFrameTime"/> counts as if it took that long, so a breakpoint, a
/// window drag or a stalled device does not produce a burst of steps that makes the frame after it even longer.
/// </para>
/// <para>
/// The leftover of the most recent frame is <see cref="Alpha"/>, the fraction of a step that has already elapsed. A
/// renderer that interpolates reads it to draw a position between the last step and the next one.
/// </para>
/// <para>
/// The game loops of the engine own an instance of this type, so a game only creates one to change the step. Register
/// it in the container before the loop is resolved, or pass it to <see cref="TestGameLoop"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var timestep = new FixedTimestep(TimeSpan.FromSeconds(1d / 120d));
/// services.AddSingleton(timestep);
/// </code>
/// </example>
public sealed class FixedTimestep
{
    /// <summary>The step that the container registers by default: one sixtieth of a second.</summary>
    public static readonly TimeSpan DefaultStep = TimeSpan.FromSeconds(1d / 60d);

    /// <summary>The longest frame time that the container takes into account by default: one quarter of a second.</summary>
    public static readonly TimeSpan DefaultMaxFrameTime = TimeSpan.FromSeconds(0.25d);

    private readonly double _stepSeconds;
    private readonly double _maxFrameSeconds;
    private double _accumulator;
    private double _elapsed;

    /// <summary>Initializes a step with the default limit on the frame time.</summary>
    /// <param name="step">The length of a step. Must be greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="step"/> is zero or negative.</exception>
    public FixedTimestep(TimeSpan step)
        : this(step, DefaultMaxFrameTime)
    {
    }

    /// <summary>Initializes a step.</summary>
    /// <param name="step">The length of a step. Must be greater than zero.</param>
    /// <param name="maxFrameTime">The longest frame time that is taken into account. Must not be shorter than <paramref name="step"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="step"/> is zero or negative, or <paramref name="maxFrameTime"/> is shorter than <paramref name="step"/>.</exception>
    public FixedTimestep(TimeSpan step, TimeSpan maxFrameTime)
    {
        if (step <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(step), step, "The step must be greater than zero.");
        }

        if (maxFrameTime < step)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFrameTime), maxFrameTime, "The longest frame time must not be shorter than the step.");
        }

        Step = step;
        MaxFrameTime = maxFrameTime;
        _stepSeconds = step.TotalSeconds;
        _maxFrameSeconds = maxFrameTime.TotalSeconds;
    }

    /// <summary>Gets the length of a step. Every step that <see cref="Advance"/> reports has this delta.</summary>
    public TimeSpan Step { get; }

    /// <summary>Gets the longest frame time that is taken into account. A longer frame is clamped to it.</summary>
    public TimeSpan MaxFrameTime { get; }

    /// <summary>Gets the fraction of a step that is left over from the most recent call, between zero and one.</summary>
    public double Alpha => _accumulator / _stepSeconds;

    /// <summary>Gets the simulated time that the steps reported so far add up to, in seconds.</summary>
    public double Elapsed => _elapsed;

    /// <summary>Adds the time that a frame took to the accumulator and reports as many whole steps as it covers.</summary>
    /// <param name="frame">The time of the frame, as the loop measured it. A delta that is negative, not a number, or longer than <see cref="MaxFrameTime"/> is clamped.</param>
    /// <param name="update">Called once per step with a delta of <see cref="Step"/> and a running total.</param>
    /// <returns>The number of steps that were reported, which is zero when the frame was shorter than the leftover of a step.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="update"/> is null.</exception>
    public int Advance(in GameTime frame, Action<GameTime> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        _accumulator += double.IsNaN(frame.Delta) ? 0d : Math.Clamp(frame.Delta, 0d, _maxFrameSeconds);

        int steps = 0;
        while (_accumulator >= _stepSeconds)
        {
            _accumulator -= _stepSeconds;
            _elapsed += _stepSeconds;
            update(new GameTime(_stepSeconds, _elapsed));
            steps++;
        }

        return steps;
    }
}
