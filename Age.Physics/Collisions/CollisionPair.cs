using Age.Core;

namespace Age.Physics;

/// <summary>
/// An unordered pair of colliding entities. The invariant A.Id is less than B.Id holds.
/// </summary>
/// <param name="A">The entity with the smaller identifier.</param>
/// <param name="B">The entity with the larger identifier.</param>
public readonly record struct CollisionPair(Entity A, Entity B);
