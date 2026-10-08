using Age.Core;

namespace Age.Physics;

/// <summary>
/// Registers the components that live in the physics assembly.
/// </summary>
internal sealed class PhysicsComponentRegistrations : IComponentRegistrations
{
    /// <inheritdoc />
    public void Register(ComponentRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register("Collider", AgePhysicsJsonContext.Default.ColliderComponent);
        registry.Register("Collision", AgePhysicsJsonContext.Default.CollisionComponent);
    }
}
