using Age.Core;

namespace Age.Physics;

/// <summary>Raised for every pair of colliders that overlap in a step.</summary>
/// <param name="First">The entity with the smaller identifier.</param>
/// <param name="Second">The entity with the larger identifier.</param>
public readonly record struct CollisionEvent(Entity First, Entity Second);
