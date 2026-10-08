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
/// The same instance also carries the state of the clock: <see cref="Paused"/> stops the simulation without stopping the
/// frames, <see cref="TimeScale"/> stretches or compresses the time that the frames contribute, and <see cref="Tick"/>
/// counts the steps. Everything a game draws or smooths on the time of the frame, rather than on the time of the
/// simulation, belongs in an <see cref="IFrameSystem"/>.
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
    private double _timeScale = 1d;
    private int _tick;

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

    /// <summary>Gets the number of steps that <see cref="Advance"/> has reported so far, which is the tick of the simulation.</summary>
    /// <remarks>
    /// A tick is a step of the simulation, not a frame of the display: a paused clock reports none, and a long frame
    /// reports several. A game that records commands or replays a run keeps them in terms of this number.
    /// </remarks>
    public int Tick => _tick;

    /// <summary>Gets or sets a value that stops the simulation: a paused clock reports no steps and ignores the time of the frames that pass.</summary>
    /// <remarks>
    /// Pausing is not the same as dropping the frames: the render callback of the loop still runs, and so does everything
    /// that a game drives from it, such as <see cref="IFrameSystem"/>. The frame time of a paused frame is discarded
    /// rather than accumulated, so a game that was paused for a minute does not resume by running a minute of steps. A
    /// pause that a game sets from inside a step stops the remaining steps of that frame and drops what was left of its
    /// time, so the simulation stops where it was paused.
    /// <see cref="Elapsed"/> and <see cref="Tick"/> stand still while the clock is paused.
    /// </remarks>
    public bool Paused { get; set; }

    /// <summary>Gets or sets the factor that the time of a frame is scaled by before it accumulates.</summary>
    /// <remarks>
    /// A game uses it for slow motion and for fast forward: a factor of 0.5 advances the simulation by half of the time
    /// the frames took, and a factor of 2 by twice as much. The factor is not the step, which stays what it was.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative, not a number, or infinite.</exception>
    public double TimeScale
    {
        get => _timeScale;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The time scale must be a finite value of zero or more.");
            }

            _timeScale = value;
        }
    }

    /// <summary>Gets the simulated time that the steps reported so far add up to, in seconds.</summary>
    public double Elapsed => _elapsed;

    /// <summary>Adds the time that a frame took to the accumulator and reports as many whole steps as it covers.</summary>
    /// <param name="frame">The time of the frame, as the loop measured it. A delta that is negative or not a number counts as zero, and one longer than <see cref="MaxFrameTime"/> is clamped.</param>
    /// <param name="update">Called once per step with a delta of <see cref="Step"/> and a running total.</param>
    /// <returns>The number of steps that were reported, which is zero when the frame was shorter than the leftover of a step or when the clock is <see cref="Paused"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="update"/> is null.</exception>
    /// <remarks>
    /// The delta of the frame is scaled by <see cref="TimeScale"/> before it accumulates. Every step that is reported
    /// grows <see cref="Tick"/> and <see cref="Elapsed"/>, so both describe the simulation rather than the display. A
    /// game that pauses the clock from inside a step stops the remaining steps of this call. A scaled delta that is not
    /// a number, which an endless frame with a <see cref="TimeScale"/> of zero produces, counts as no time at all.
    /// </remarks>
    public int Advance(in GameTime frame, Action<GameTime> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (Paused)
        {
            _accumulator = 0d;
            return 0;
        }

        double delta = double.IsNaN(frame.Delta) ? 0d : frame.Delta * _timeScale;

        // A frame that never ends, scaled by a time scale of zero, is not a number: it counts as no time at all instead
        // of poisoning the accumulator for good. The clamp keeps an endless frame at the maximum frame time.
        _accumulator += double.IsNaN(delta) ? 0d : Math.Clamp(delta, 0d, _maxFrameSeconds);

        int steps = 0;
        while (_accumulator >= _stepSeconds)
        {
            _accumulator -= _stepSeconds;
            _elapsed += _stepSeconds;
            _tick++;
            update(new GameTime(_stepSeconds, _elapsed));
            steps++;

            if (Paused)
            {
                // The game paused itself while the step ran, so the simulation stops in that step rather than at the end
                // of the frame, and what is left of the frame is dropped instead of becoming steps after a resume.
                _accumulator = 0d;
                break;
            }
        }

        return steps;
    }
}
