using Age.Core;

namespace Age.Physics;

/// <summary>
/// Marks an entity that overlapped another one during the last collision update. Only the first collision is kept.
/// </summary>
/// <remarks>
/// The component is removed at the start of every update and re-attached to the entities that collide, so it always
/// describes the current frame. Because an entity stores a single partner, use <see cref="CollisionSystem.LastPairs"/>
/// when every overlap matters.
/// </remarks>
public struct CollisionComponent : IComponent
{
    /// <summary>Gets or sets the one entity recorded here: the first partner this entity collided with during the update.</summary>
    public Entity Other;
}
