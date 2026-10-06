using Age.Core;

namespace Age.Physics;

/// <summary>
/// Defines the axis-aligned collision box of an entity, relative to its <see cref="TransformComponent"/>.
/// </summary>
/// <remarks>
/// The box is built from the transform position, the offset and the size scaled by the transform scale. The rotation of
/// the transform is ignored: collision detection is axis-aligned only, so a rotated sprite still collides with an upright
/// box. Oriented boxes are on the roadmap.
/// </remarks>
public struct ColliderComponent : IComponent
{
    /// <summary>Gets or sets the size of the box, before the transform scale is applied.</summary>
    public Vector2 Size;

    /// <summary>Gets or sets the offset from the transform position to the top-left corner of the box.</summary>
    public Vector2 Offset;
}
