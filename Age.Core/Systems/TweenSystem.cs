namespace Age.Core;

/// <summary>
/// Advances the tweens of a world by the time of every step and announces the value each one has reached.
/// </summary>
/// <remarks>
/// The system reads the tweens into a buffer before it writes, because the world must not change while its components
/// are being enumerated, and a tween may be removed or replaced by whoever reacts to it.
/// </remarks>
public sealed class TweenSystem : ISystem
{
    private readonly List<Entity> _tweens = new();

    /// <inheritdoc />
    /// <remarks>
    /// A tween that does not loop keeps the value of its last step and stops announcing, so the last event a game sees
    /// of it is the one that reports it as completed.
    /// </remarks>
    public void Update(World world, in GameTime time)
    {
        ArgumentNullException.ThrowIfNull(world);

        _tweens.Clear();

        foreach (Entity entity in world.Enumerate<TweenComponent>())
        {
            if (!world.Get<TweenComponent>(entity).Paused)
            {
                _tweens.Add(entity);
            }
        }

        foreach (Entity entity in _tweens)
        {
            TweenComponent tween = world.Get<TweenComponent>(entity);

            if (tween.Completed)
            {
                continue;
            }

            tween.Elapsed += (float)time.Delta;

            if (tween.Looping && tween.Elapsed >= tween.Duration)
            {
                tween.Elapsed = tween.Duration > 0f ? tween.Elapsed % tween.Duration : 0f;
            }

            world.Set(entity, tween);
            world.Events.Raise(new TweenUpdatedEvent(entity, tween.Value, tween.Progress, tween.Completed));
        }
    }
}
