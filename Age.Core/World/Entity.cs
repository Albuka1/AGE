namespace Age.Core;

/// <summary>
/// Identifies an entity that lives inside a world.
/// </summary>
/// <remarks>
/// An identifier is only meaningful for the world that created it. It records the slot and the generation of that slot,
/// so a default value and an identifier left behind when its entity was destroyed both fail <see cref="World.IsAlive"/>
/// instead of naming whichever entity took the slot next. Only <see cref="World.CreateEntity"/> creates identifiers,
/// because the constructor is not public.
/// </remarks>
/// <example>
/// <code>
/// Entity target = world.CreateEntity();
///
/// world.DestroyEntity(target);
/// if (!world.IsAlive(target))
/// {
///     // the identifier went stale: the slot may already belong to another entity
/// }
/// </code>
/// </example>
public readonly struct Entity : IEquatable<Entity>
{
    /// <summary>Initializes an identifier. Only a <see cref="World"/> creates identifiers, so a caller cannot forge one.</summary>
    internal Entity(int id, int generation)
    {
        Id = id;
        Generation = generation;
    }

    /// <summary>Gets the identifier of the slot. The first slot is zero, and a default entity names the slot before it.</summary>
    public int Id { get; }

    /// <summary>Gets the generation of the slot. It grows every time the slot is handed out again, which is what makes the identifier of a destroyed entity stale.</summary>
    public int Generation { get; }

    /// <summary>Determines whether two identifiers name the same entity, in the same generation.</summary>
    public static bool operator ==(Entity left, Entity right) => left.Equals(right);

    /// <summary>Determines whether two identifiers are not equal.</summary>
    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);

    /// <summary>Determines whether this identifier equals another identifier.</summary>
    public bool Equals(Entity other) => Id == other.Id && Generation == other.Generation;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Entity other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Id, Generation);

    /// <inheritdoc />
    public override string ToString() => $"Entity {Id} (generation {Generation})";
}
