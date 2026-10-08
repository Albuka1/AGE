namespace Age.Core;

/// <summary>
/// A base for systems that react to events. A system declares what it listens to in <see cref="Subscribe"/> and does its
/// work per step in <see cref="OnUpdate"/>.
/// </summary>
/// <remarks>
/// <para>
/// The bus is not in the container: it belongs to the world, which the caller owns, so a system subscribes the first
/// time it is updated and <see cref="Subscribe"/> therefore runs once. This is why a game registers a system in the
/// pipeline and not in the container: whoever owns the world owns its systems.
/// </para>
/// <para>
/// A disabled system keeps its subscriptions and simply stops updating, which is what a pause menu wants: the rules stay
/// in place while the simulation stands still, and switching <see cref="Enabled"/> back resumes them.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class DamageSystem : EntitySystem
/// {
///     protected override void Subscribe(EventBus events) =>
///         events.Subscribe&lt;CollisionEvent&gt;((_, collision) =&gt; Console.WriteLine(collision.A));
///
///     protected override void OnUpdate(World world, in GameTime time)
///     {
///     }
/// }
/// </code>
/// </example>
public abstract class EntitySystem : ISystem
{
    private bool _subscribed;

    /// <summary>Gets or sets a value that stops the system from updating while leaving its subscriptions in place.</summary>
    public bool Enabled { get; set; } = true;

    /// <inheritdoc />
    public void Update(World world, in GameTime time)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!_subscribed)
        {
            Subscribe(world.Events);
            _subscribed = true;
        }

        if (Enabled)
        {
            OnUpdate(world, time);
        }
    }

    /// <summary>Declares what the system reacts to. Called once, before the first update, with the bus of the world.</summary>
    /// <param name="events">The bus of the world the system runs in.</param>
    protected virtual void Subscribe(EventBus events)
    {
    }

    /// <summary>Advances the system by one step, which happens only while <see cref="Enabled"/> is true.</summary>
    /// <param name="world">The world to read and to change.</param>
    /// <param name="time">The time of the step.</param>
    protected abstract void OnUpdate(World world, in GameTime time);
}
