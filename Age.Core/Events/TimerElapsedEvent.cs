namespace Age.Core;

/// <summary>Raised when a timer of an entity ran out at the end of a step.</summary>
/// <param name="Entity">The entity that carries the timer.</param>
/// <param name="Repeating">A value indicating whether the timer started another run instead of stopping.</param>
/// <remarks>
/// A timer that repeats reports once per run, so a handler that only cares about the last run looks at
/// <paramref name="Repeating"/> or removes the timer when it is done.
/// </remarks>
public readonly record struct TimerElapsedEvent(Entity Entity, bool Repeating);
