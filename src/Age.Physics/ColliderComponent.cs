using Age.Core;

namespace Age.Physics;

/// <summary>
/// Defines an axis-aligned collision box relative to an entity transform.
/// </summary>
public struct ColliderComponent : IComponent
{
    /// <summary>Gets or sets the size of the box.</summary>
    public Vector2 Size;

    /// <summary>Gets or sets the offset from the entity position to the box origin.</summary>
    public Vector2 Offset;
}
