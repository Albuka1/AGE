namespace Age.Core;

/// <summary>
/// What a game says about the engine it was written for: the version of the engine that the game was built against.
/// </summary>
/// <remarks>
/// A game ships this beside its executable in a <c>game.version.json</c>, and the engine compares it with its own version before the
/// game starts. It is a small document on purpose: the point is that a game says what it was written for, not that it describes
/// itself.
/// </remarks>
/// <example>
/// <code>
/// {
///   "engine": "0.4.0"
/// }
/// </code>
/// </example>
public sealed record GameVersion
{
    /// <summary>The name of the file a game ships its declaration in, beside its executable.</summary>
    public const string FileName = "game.version.json";

    /// <summary>Gets the version of the engine that the game was written for, as three numbers such as <c>0.4.0</c>.</summary>
    public required string Engine { get; init; }
}

/// <summary>
/// How the version of the engine that a game declares agrees with the one that is running it, which is what a game refuses to start
/// over.
/// </summary>
public enum VersionMatch
{
    /// <summary>The game declares the version of the engine that is running it.</summary>
    Same,

    /// <summary>The game declares another minor or patch of the same major version, which is run with a warning.</summary>
    Compatible,

    /// <summary>The game declares another major version, which is not run at all.</summary>
    Different,
}
