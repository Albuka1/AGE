using Age.Core;

namespace Age.Rendering;

/// <summary>Raised when a state of a sprite sheet that does not loop reached its last frame.</summary>
/// <param name="Entity">The entity that carries the animation.</param>
/// <param name="State">The name of the state that finished.</param>
/// <remarks>
/// The animation of the entity stands paused when the event is raised, so a handler that plays the next state sets
/// <c>State</c> and clears <c>Paused</c>, and one that is done with the character removes the component.
/// </remarks>
public readonly record struct SpriteAnimationFinishedEvent(Entity Entity, string State);
