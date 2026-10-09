using Age.Core;

namespace Age.Content.Prototypes;

/// <summary>
/// Creates entities out of the content of a game: a spawn takes the identifier of a prototype, makes an entity in a world
/// and attaches the components that the prototype declares.
/// </summary>
/// <remarks>
/// <para>
/// This is what makes a map, an enemy or an item a document rather than a class: a game asks for the identifier of a
/// prototype and receives an entity that carries what that prototype holds, with the values it declares. The components
/// are read by the same contract that reads a scene, so a value in a document of content and a value in a saved map mean
/// the same thing.
/// </para>
/// <para>
/// The service holds no world of its own: a game may run more than one, and the world is what a spawn writes to.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// Entity goblin = spawner.Spawn(world, "Goblin", new Vector2(320f, 240f));
/// </code>
/// </example>
public sealed class SpawnService
{
    private readonly IPrototypeManager _prototypes;
    private readonly ComponentRegistry _components;

    /// <summary>Initializes the service with the content of a game and the registry that reads its components.</summary>
    /// <param name="prototypes">The content, which has to hold the kinds a game spawns.</param>
    /// <param name="components">The registry of the components of the engine and the game.</param>
    /// <exception cref="ArgumentNullException">The content or the registry is null.</exception>
    public SpawnService(IPrototypeManager prototypes, ComponentRegistry components)
    {
        ArgumentNullException.ThrowIfNull(prototypes);
        ArgumentNullException.ThrowIfNull(components);

        _prototypes = prototypes;
        _components = components;
    }

    /// <summary>Creates an entity in a world and attaches the components of a prototype.</summary>
    /// <param name="world">The world to create the entity in.</param>
    /// <param name="prototypeId">The identifier of the prototype the entity is made of.</param>
    /// <returns>The entity, which carries what the prototype declares.</returns>
    /// <exception cref="ArgumentNullException">The world is null.</exception>
    /// <exception cref="ArgumentException">The identifier is null, empty or whitespace.</exception>
    /// <exception cref="KeyNotFoundException">The content holds no entity under that identifier.</exception>
    /// <exception cref="InvalidOperationException">A component of the prototype is not registered, or its values cannot be read.</exception>
    public Entity Spawn(World world, string prototypeId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(prototypeId);

        return Spawn(world, _prototypes.Get<EntityPrototype>(prototypeId));
    }

    /// <summary>Creates an entity in a world, attaches the components of a prototype, and places it.</summary>
    /// <param name="world">The world to create the entity in.</param>
    /// <param name="prototypeId">The identifier of the prototype the entity is made of.</param>
    /// <param name="position">The position to write into the transform of the entity, when the prototype gives it one.</param>
    /// <returns>The entity, which carries what the prototype declares and stands where the caller asked.</returns>
    /// <exception cref="ArgumentNullException">The world is null.</exception>
    /// <exception cref="ArgumentException">The identifier is null, empty or whitespace.</exception>
    /// <exception cref="KeyNotFoundException">The content holds no entity under that identifier.</exception>
    /// <exception cref="InvalidOperationException">A component of the prototype is not registered, or its values cannot be read.</exception>
    /// <remarks>A prototype that carries no transform is placed nowhere, which is what a marker of a map or a hidden thing is.</remarks>
    public Entity Spawn(World world, string prototypeId, Vector2 position)
    {
        ArgumentNullException.ThrowIfNull(world);

        Entity entity = Spawn(world, prototypeId);

        if (world.Has<TransformComponent>(entity))
        {
            world.GetRef<TransformComponent>(entity).Position = position;
        }

        return entity;
    }

    /// <summary>Creates an entity from a prototype when the content holds it.</summary>
    /// <param name="world">The world to create the entity in.</param>
    /// <param name="prototypeId">The identifier of the prototype the entity is made of.</param>
    /// <param name="entity">Receives the entity when the content holds the prototype.</param>
    /// <returns><see langword="true"/> when the content holds an entity under that identifier.</returns>
    /// <exception cref="ArgumentNullException">The world is null.</exception>
    /// <exception cref="ArgumentException">The identifier is null, empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">A component of the prototype is not registered, or its values cannot be read.</exception>
    public bool TrySpawn(World world, string prototypeId, out Entity entity)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(prototypeId);

        if (_prototypes.TryGet(prototypeId, out EntityPrototype? prototype))
        {
            entity = Spawn(world, prototype);
            return true;
        }

        entity = default;
        return false;
    }

    /// <summary>Creates an entity and attaches the components of a prototype that was resolved already.</summary>
    /// <remarks>
    /// Every component of the prototype is read before anything of it reaches the world, and an entity that could not
    /// take one of them goes away again, so a spawn that fails leaves the world as it was rather than holding half of a
    /// thing that no document describes.
    /// </remarks>
    private Entity Spawn(World world, EntityPrototype prototype)
    {
        var components = new List<(string Name, object Values)>(prototype.Components.Count);

        foreach (PrototypeComponent component in prototype.Components)
        {
            if (!_components.TryDeserialize(component.Name, component.Values, out object? values))
            {
                throw new InvalidOperationException($"The component '{component.Name}' of '{prototype.Id}' is not registered, so nothing can attach it ({component.File}, line {component.Line}).");
            }

            components.Add((component.Name, values));
        }

        Entity entity = world.CreateEntity();

        try
        {
            foreach ((string name, object values) in components)
            {
                _components.TryApply(name, world, entity, values);
            }
        }
        catch
        {
            world.DestroyEntity(entity);
            throw;
        }

        return entity;
    }
}
