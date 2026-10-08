namespace Age.Core;

/// <summary>
/// Counts down a duration on the entity it is attached to, which a <see cref="TimerSystem"/> advances.
/// </summary>
/// <remarks>
/// <para>
/// The timer counts the time of the simulation, so it stands still while the clock is paused, and it keeps the leftover
/// of the step that ran past the end: a step of a tenth of a second that lands on a timer with a hundredth left reports
/// the event at once and starts the next run from the leftover rather than from the whole duration.
/// </para>
/// <para>
/// A timer that ran out once stops instead of removing itself, so the entity keeps the record of what happened: a game
/// removes the component or sets <see cref="Paused"/> to start it again. A timer built by hand needs both
/// <see cref="Duration"/> and <see cref="Remaining"/>; <see cref="For"/> sets both.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// world.Set(effect, TimerComponent.For(2f));
///
/// world.Events.Subscribe&lt;TimerElapsedEvent&gt;((entity, _) =&gt; world.RequestDestroy(entity));
/// </code>
/// </example>
public struct TimerComponent : IComponent
{
    /// <summary>Gets or sets how long one run takes, in seconds. A duration of zero or less runs out on the next step.</summary>
    public float Duration { get; set; }

    /// <summary>Gets or sets the time that is left of the current run, in seconds. The system writes it.</summary>
    public float Remaining { get; set; }

    /// <summary>Gets or sets a value indicating whether the timer starts another run after it runs out.</summary>
    public bool Repeat { get; set; }

    /// <summary>Gets or sets a value indicating whether the timer stands still. Cancelling a timer sets it to true.</summary>
    public bool Paused { get; set; }

    /// <summary>Returns a timer that starts a run of the given length.</summary>
    /// <param name="duration">The length of one run, in seconds.</param>
    /// <param name="repeat">A value indicating whether the timer starts over after every run.</param>
    /// <returns>The timer, ready to be attached to an entity.</returns>
    public static TimerComponent For(float duration, bool repeat = false) =>
        new() { Duration = duration, Remaining = duration, Repeat = repeat };
}
