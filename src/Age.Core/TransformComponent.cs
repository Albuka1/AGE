namespace Age.Core;

/// <summary>
/// Stores the position, rotation and scale of an entity in world space.
/// </summary>
public struct TransformComponent : IComponent
{
    /// <summary>Gets or sets the position of the top-left corner in world coordinates.</summary>
    public Vector2 Position;

    /// <summary>Gets or sets the rotation of the entity around the centre of its quad, in radians.</summary>
    /// <remarks>
    /// The renderer turns the quad of a sprite by this angle. Collision detection works on axis-aligned boxes, so a
    /// rotation changes the picture but not what the colliders of the physics assembly report.
    /// </remarks>
    public float Rotation;

    /// <summary>Gets or sets the scale applied to the entity.</summary>
    public Vector2 Scale;
}
