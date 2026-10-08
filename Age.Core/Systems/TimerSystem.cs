namespace Age.Core;

/// <summary>
/// Counts the timers of a world down by the time of every step and announces the ones that run out.
/// </summary>
/// <remarks>
/// The system reads the timers into a buffer before it writes, because the world must not change while its components
/// are being enumerated, and a timer that runs out may be removed or restarted by whoever reacts to it. A repeating timer
/// reports every run that a step consumed, and a run of no length reports once per step, so the timers of a world never
/// fall behind the time they count, whatever the length of a step.
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

            if (!timer.Repeat)
            {
                timer.Remaining = 0f;
                timer.Paused = true;
                world.Set(entity, timer);
                world.Events.Raise(new TimerElapsedEvent(entity, Repeating: false));
                continue;
            }

            // A repeating timer catches up when a step is longer than a run: every run that the step consumed reports its
            // own event, and the deficit of the step carries into the run behind it, so the average of the duration holds
            // instead of the timer sinking a step further behind on every step.
            int runs;

            if (timer.Duration > 0f)
            {
                runs = 0;

                do
                {
                    timer.Remaining += timer.Duration;
                    runs++;
                }
                while (timer.Remaining <= 0f);
            }
            else
            {
                // A run of no length is over as soon as it starts: the timer reports once for this step and stands at zero
                // rather than sinking by a step on every one.
                timer.Remaining = 0f;
                runs = 1;
            }

            world.Set(entity, timer);

            for (int run = 0; run < runs; run++)
            {
                world.Events.Raise(new TimerElapsedEvent(entity, Repeating: true));
            }
        }
    }
}
