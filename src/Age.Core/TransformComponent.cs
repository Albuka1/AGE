namespace Age.Core;

/// <summary>
/// Stores the position, rotation and scale of an entity in world space.
/// </summary>
public struct TransformComponent : IComponent
{
    /// <summary>Gets or sets the position of the top-left corner in world coordinates.</summary>
    public Vector2 Position;

    /// <summary>Gets or sets the rotation, in radians.</summary>
    public float Rotation;

    /// <summary>Gets or sets the scale applied to the entity.</summary>
    public Vector2 Scale;
}
