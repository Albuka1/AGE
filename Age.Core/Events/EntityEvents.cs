namespace Age.Core;

/// <summary>Raised when an entity becomes part of the world.</summary>
/// <param name="Entity">The entity that appeared.</param>
public readonly record struct EntityCreatedEvent(Entity Entity);

/// <summary>Raised when an entity is destroyed, after its components are gone.</summary>
/// <param name="Entity">The entity that went away.</param>
public readonly record struct EntityDestroyedEvent(Entity Entity);

/// <summary>Raised when a component of type <typeparamref name="T"/> is attached to an entity.</summary>
/// <typeparam name="T">The component type, which only names the event: the component itself is on the entity.</typeparam>
/// <param name="Entity">The entity that received the component.</param>
public readonly record struct ComponentAddedEvent<T>(Entity Entity) where T : struct, IComponent;

/// <summary>Raised when a component of type <typeparamref name="T"/> is removed from an entity.</summary>
/// <typeparam name="T">The component type, which only names the event: the component itself is already gone.</typeparam>
/// <param name="Entity">The entity that lost the component.</param>
public readonly record struct ComponentRemovedEvent<T>(Entity Entity) where T : struct, IComponent;
