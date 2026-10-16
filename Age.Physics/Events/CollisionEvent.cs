using Age.Core;

namespace Age.Physics;

/// <summary>Raised for every pair of colliders that overlap in a step.</summary>
/// <param name="First">The entity with the smaller identifier.</param>
/// <param name="Second">The entity with the larger identifier.</param>
/// <param name="IsTrigger">A value indicating whether one of the two colliders of the pair is a trigger.</param>
/// <remarks>
/// The event is raised on every step that the pair overlaps rather than on the step the two met, which is what a body
/// that spends a whole step inside a zone reads. A game that wants the first step of a contact reads
/// <see cref="CollisionEnterEvent"/> and one that wants the step that ended it reads <see cref="CollisionExitEvent"/>.
/// </remarks>
public readonly record struct CollisionEvent(Entity First, Entity Second, bool IsTrigger);

/// <summary>Raised on the step two colliders began to overlap, which is the first step of a contact.</summary>
/// <param name="First">The entity with the smaller identifier.</param>
/// <param name="Second">The entity with the larger identifier.</param>
/// <param name="IsTrigger">A value indicating whether one of the two colliders of the pair is a trigger.</param>
/// <remarks>
/// A pair that is delivered in one step as a <see cref="CollisionEvent"/> and in the next step as nothing at all is a pair
/// that was never seen by a game that only read this event: the two colliders overlapped for one step and stopped, which
/// is what a body that moves through a zone of one cell of a grid does.
/// </remarks>
public readonly record struct CollisionEnterEvent(Entity First, Entity Second, bool IsTrigger);

/// <summary>Raised on the step two colliders stopped overlapping, which is the first step without a contact.</summary>
/// <param name="First">The entity with the smaller identifier.</param>
/// <param name="Second">The entity with the larger identifier.</param>
public readonly record struct CollisionExitEvent(Entity First, Entity Second);

