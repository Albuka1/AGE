namespace Age.Core;

/// <summary>How long one system took during the last step or the last frame.</summary>
/// <param name="Name">The name of the system type, which is what a developer recognizes the system by.</param>
/// <param name="Milliseconds">The time the system spent, in milliseconds.</param>
public readonly record struct SystemTiming(string Name, double Milliseconds);
