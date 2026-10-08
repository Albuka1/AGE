using System.Text.Json.Serialization;

namespace Age.Core;

/// <summary>
/// A reference to another entity that survives a save and a load.
/// </summary>
/// <remarks>
/// <para>
/// A component cannot hold an <see cref="Entity"/> across a save: the loaded world hands out its own slots, so a stored
/// slot would name whichever entity takes it next. An <see cref="EntityRef"/> holds the identifier that the scene knows
/// the entity by instead (<see cref="SceneId"/>), which a world keeps stable, and a load maps that number back to the
/// entity it created.
/// </para>
/// <para>
/// The value is written as a plain number, so a scene stays readable and editable by hand. The default value names no
/// entity, which is what <see cref="None"/> is, and a reference to an entity that a world does not know resolves to the
/// default entity rather than to a wrong one.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// world.Set(unit, new TargetComponent { Target = world.Reference(enemy) });
///
/// Entity target = world.Resolve(world.Get&lt;TargetComponent&gt;(unit).Target);
///
/// if (world.IsAlive(target))
/// {
///     // the reference still names an entity that exists
/// }
/// </code>
/// </example>
[JsonConverter(typeof(EntityRefJsonConverter))]
public readonly struct EntityRef : IEquatable<EntityRef>
{
    /// <summary>Initializes a reference to the entity that a scene knows under the given identifier.</summary>
    /// <param name="sceneId">The identifier of the entity in its scene. Zero means that it names no entity.</param>
    public EntityRef(int sceneId) => SceneId = sceneId;

    /// <summary>Gets the reference that names no entity.</summary>
    public static EntityRef None => default;

    /// <summary>Gets the identifier of the referenced entity in its scene, or zero when it names none.</summary>
    public int SceneId { get; }

    /// <summary>Gets a value indicating whether the reference names an entity at all.</summary>
    public bool HasValue => SceneId != 0;

    /// <summary>Returns the entity that the world knows under the identifier of this reference.</summary>
    /// <param name="world">The world to ask.</param>
    /// <returns>The entity, or the default one when the world holds none under that identifier.</returns>
    /// <exception cref="ArgumentNullException">The world is null.</exception>
    /// <remarks>
    /// A reference whose entity is gone resolves to the default entity, which <see cref="World.IsAlive"/> refuses, so a
    /// scene that refers to an entity it does not hold cannot be mistaken for a working one.
    /// </remarks>
    public Entity Resolve(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.TryEntityOf(SceneId, out Entity entity) ? entity : default;
    }

    /// <summary>Determines whether the referenced entity exists in the world.</summary>
    /// <param name="world">The world to ask.</param>
    /// <returns><see langword="true"/> when the world holds the entity and it is alive.</returns>
    /// <exception cref="ArgumentNullException">The world is null.</exception>
    public bool IsAlive(World world) => world.IsAlive(Resolve(world));

    /// <summary>Determines whether two references name the same entity.</summary>
    public bool Equals(EntityRef other) => SceneId == other.SceneId;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is EntityRef other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => SceneId;

    /// <summary>Determines whether two references name the same entity.</summary>
    public static bool operator ==(EntityRef left, EntityRef right) => left.Equals(right);

    /// <summary>Determines whether two references name different entities.</summary>
    public static bool operator !=(EntityRef left, EntityRef right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => SceneId == 0 ? "EntityRef (none)" : $"EntityRef {SceneId}";
}
