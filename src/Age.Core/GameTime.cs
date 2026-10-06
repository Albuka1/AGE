namespace Age.Core;

/// <summary>
/// Describes the time elapsed since the previous update.
/// </summary>
/// <param name="Delta">The time elapsed since the previous update, in seconds.</param>
/// <param name="Total">The total time elapsed since the start of the loop, in seconds.</param>
public readonly record struct GameTime(double Delta, double Total);
