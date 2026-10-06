namespace Age.Core;

/// <summary>
/// Identifies an entity that lives inside a world.
/// </summary>
public readonly struct Entity
{
    /// <summary>
    /// Gets the numeric identifier of the entity.
    /// </summary>
    public int Id { get; }

    internal Entity(int id) => Id = id;
}
