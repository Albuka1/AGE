namespace Age.Core;

/// <summary>
/// Delivers events to the systems and the game code that subscribed to them.
/// </summary>
/// <remarks>
/// <para>
/// An event is a value. Raising one does not hand it to the subscribers right away: the bus queues it and
/// <see cref="Dispatch"/> delivers the queue. <see cref="World.Update"/> and <see cref="World.UpdateFrame"/> call that
/// at the boundaries of the step, next to <see cref="World.ApplyPending"/>, so a handler never runs in the middle of a
/// system that is changing the world, and the events of a step are all delivered before the next step starts.
/// </para>
/// <para>
/// An event that a handler raises waits for the next boundary. That keeps a chain of reactions from running away: a
/// handler can tell that something happened and react without the reaction turning into an endless cascade inside the
/// same step.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// world.Events.Subscribe&lt;CollisionEvent&gt;((entity, collision) =&gt; Console.WriteLine(collision.First));
/// world.Events.Raise(new CollisionEvent(first, second));
/// </code>
/// </example>
public sealed class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private readonly List<QueuedEvent> _queue = new();

    /// <summary>Adds a handler for every event of type <typeparamref name="TEvent"/>.</summary>
    /// <typeparam name="TEvent">The event type, which is a value.</typeparam>
    /// <param name="handler">The handler. Its entity is the one the event was raised for, or the default one for a broadcast.</param>
    /// <exception cref="ArgumentNullException">The handler is null.</exception>
    public void Subscribe<TEvent>(Action<Entity, TEvent> handler) where TEvent : struct
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (!_handlers.TryGetValue(typeof(TEvent), out List<Delegate>? handlers))
        {
            handlers = [];
            _handlers[typeof(TEvent)] = handlers;
        }

        handlers.Add(handler);
    }

    /// <summary>Removes a handler that <see cref="Subscribe{TEvent}"/> added.</summary>
    /// <typeparam name="TEvent">The event type the handler was added for.</typeparam>
    /// <param name="handler">The handler to remove.</param>
    /// <returns><see langword="true"/> when the handler was subscribed and has been removed.</returns>
    public bool Unsubscribe<TEvent>(Action<Entity, TEvent> handler) where TEvent : struct =>
        _handlers.TryGetValue(typeof(TEvent), out List<Delegate>? handlers) && handlers.Remove(handler);

    /// <summary>Queues an event for every subscriber of its type, which is not directed at one entity.</summary>
    /// <typeparam name="TEvent">The event type, which is a value.</typeparam>
    /// <param name="event">The event to deliver.</param>
    /// <remarks>A subscriber receives it with the default entity, so a handler that needs one ignores it.</remarks>
    public void Raise<TEvent>(in TEvent @event) where TEvent : struct => _queue.Add(new QueuedEvent<TEvent>(default, @event));

    /// <summary>Queues an event for every subscriber of its type, directed at one entity.</summary>
    /// <typeparam name="TEvent">The event type, which is a value.</typeparam>
    /// <param name="entity">The entity the event is about.</param>
    /// <param name="event">The event to deliver.</param>
    public void Raise<TEvent>(Entity entity, in TEvent @event) where TEvent : struct =>
        _queue.Add(new QueuedEvent<TEvent>(entity, @event));

    /// <summary>Hands the queued events to their subscribers, in the order in which they were raised.</summary>
    /// <returns>The number of events that were delivered, which is zero when none were queued.</returns>
    /// <remarks>
    /// An event that a handler raises while this call runs is left for the next call. A handler that subscribes or
    /// unsubscribes during the delivery of an event does not change who receives that event.
    /// </remarks>
    public int Dispatch()
    {
        if (_queue.Count == 0)
        {
            return 0;
        }

        QueuedEvent[] batch = [.. _queue];
        _queue.Clear();

        foreach (QueuedEvent queued in batch)
        {
            queued.Deliver(this);
        }

        return batch.Length;
    }

    private void Deliver<TEvent>(Entity entity, in TEvent @event) where TEvent : struct
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out List<Delegate>? handlers))
        {
            return;
        }

        // A handler that subscribes or unsubscribes while this event is delivered must not change the delivery of it.
        Delegate[] subscribers = [.. handlers];

        foreach (Delegate subscriber in subscribers)
        {
            ((Action<Entity, TEvent>)subscriber)(entity, @event);
        }
    }

    private abstract class QueuedEvent
    {
        public abstract void Deliver(EventBus bus);
    }

    private sealed class QueuedEvent<TEvent> : QueuedEvent where TEvent : struct
    {
        private readonly Entity _entity;
        private readonly TEvent _event;

        public QueuedEvent(Entity entity, in TEvent @event)
        {
            _entity = entity;
            _event = @event;
        }

        public override void Deliver(EventBus bus) => bus.Deliver(_entity, _event);
    }
}
