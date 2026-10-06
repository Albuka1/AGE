using Age.Core;

namespace Age.Physics;

/// <summary>
/// Holds only the first collision. The full list is available in <see cref="CollisionSystem.LastPairs"/>.
/// </summary>
public struct CollisionComponent : IComponent
{
    /// <summary>Gets or sets the entity that first collided with the owner.</summary>
    public Entity Other;
}
