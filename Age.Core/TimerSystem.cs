namespace Age.Core;

/// <summary>
/// Counts the timers of a world down by the time of every step and announces the ones that run out.
/// </summary>
/// <remarks>
/// The system reads the timers into a buffer before it writes, because the world must not change while its components
/// are being enumerated, and a timer that runs out may be removed or restarted by whoever reacts to it.
/// </remarks>
public sealed class TimerSystem : ISystem
{
    private readonly List<Entity> _timers = new();

    /// <inheritdoc />
    /// <remarks>
    /// The time of the step is the fixed step of the clock, so a timer counts the simulation rather than the frames.
    /// The events are queued like every other event and reach their subscribers at the boundary of the step.
    /// </remarks>
    public void Update(World world, in GameTime time)
    {
        ArgumentNullException.ThrowIfNull(world);

        _timers.Clear();

        foreach (Entity entity in world.Enumerate<TimerComponent>())
        {
            if (!world.Get<TimerComponent>(entity).Paused)
            {
                _timers.Add(entity);
            }
        }

        foreach (Entity entity in _timers)
        {
            TimerComponent timer = world.Get<TimerComponent>(entity);
            timer.Remaining -= (float)time.Delta;

            if (timer.Remaining > 0f)
            {
                world.Set(entity, timer);
                continue;
            }

            if (timer.Repeat)
            {
                // The leftover of the step carries into the next run, so a repeating timer keeps the average of its
                // duration instead of drifting by up to a step per run.
                timer.Remaining += timer.Duration > 0f ? timer.Duration : 0f;
            }
            else
            {
                timer.Remaining = 0f;
                timer.Paused = true;
            }

            world.Set(entity, timer);
            world.Events.Raise(new TimerElapsedEvent(entity, timer.Repeat));
        }
    }
}
