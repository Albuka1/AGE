using Age.Core;

namespace Age.Physics;

/// <summary>
/// An unordered pair of colliding entities. The invariant A.Id is less than B.Id holds.
/// </summary>
/// <param name="A">The entity with the smaller identifier.</param>
/// <param name="B">The entity with the larger identifier.</param>
/// <param name="IsTrigger">A value indicating whether one of the two colliders of the pair is a trigger.</param>
/// <remarks>
/// A pair that holds a trigger is reported and never resolved, which is what a body of a battle on a grid of cells is:
/// the cell of the grid is what decides where a unit stands, and a box of it is a zone that a shot and a zone of effect
/// read rather than a body that a move is stopped by.
/// </remarks>
public readonly record struct CollisionPair(Entity A, Entity B, bool IsTrigger);
