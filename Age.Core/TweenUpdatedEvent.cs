namespace Age.Core;

/// <summary>Raised for every step of a running tween, with the value it has reached.</summary>
/// <param name="Entity">The entity that carries the tween.</param>
/// <param name="Value">The value at the point the tween has reached, with its easing applied.</param>
/// <param name="Progress">The fraction of the run that has passed, between zero and one.</param>
/// <param name="Completed">A value indicating whether a tween that does not loop has reached its end.</param>
/// <remarks>
/// A tween that does not loop announces its end once and then stops, so a handler that runs on <paramref name="Completed"/>
/// sees a finished tween exactly once and can remove the component there.
/// </remarks>
public readonly record struct TweenUpdatedEvent(Entity Entity, float Value, float Progress, bool Completed);
