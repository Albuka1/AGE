using System.Reflection;

namespace Age.Core;

/// <summary>
/// The version of the engine that a build of it reports, and the engine a game declares that it was built against.
/// </summary>
/// <remarks>
/// <para>
/// The version lives in one place, which is <c>&lt;Version&gt;</c> of <c>Directory.Build.props</c>: every assembly of the engine
/// carries it, and this type reads it off the assembly rather than repeating it. A game declares the engine it was written for in a
/// <c>game.version.json</c> beside its executable, and the two are compared before a game starts, so a game that was built against
/// an engine other than the one it is running on is refused with a line rather than a failure somewhere deep in a frame.
/// </para>
/// <para>
/// The comparison is on the three numbers and not on a suffix: a game written for <c>0.4.0</c> runs on <c>0.4.0-alpha.1</c>, because
/// the suffix describes the build rather than the contract the game was written against.
/// </para>
/// </remarks>
public static class EngineVersion
{
    /// <summary>The version of the engine that this assembly carries, as three numbers such as <c>0.4.0</c>.</summary>
    public static string Value { get; } = Read(typeof(EngineVersion).Assembly);

    /// <summary>Returns the version of an assembly, as three numbers, which is what a build of the engine reports.</summary>
    /// <param name="assembly">The assembly to read the version of.</param>
    /// <returns>The three numbers of the version, or <c>0.0.0</c> for an assembly that carries none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is null.</exception>
    public static string Read(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
