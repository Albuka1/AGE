using System.Text.Json;

namespace Age.Core;

/// <summary>
/// Compares the version of the engine that a game declares with the one that is running it, which is what refuses a mismatch before a
/// frame is drawn.
/// </summary>
/// <remarks>
/// <para>
/// A game reads its own <c>game.version.json</c> beside its executable and hands it here before it starts. A game that names no file
/// is one that does not say what it was written for, which is accepted; a game whose declaration names another contract is refused;
/// and a game of the same contract with a different build is run with a warning, because a version moved forward is the usual case
/// and a person reads the warning rather than a game that will not start.
/// </para>
/// <para>
/// What another contract is follows the versioning of this engine: before 1.0 the minor number carries a change a game has to follow,
/// so 0.4 and 0.5 are not the same contract and 0.4.0 and 0.4.3 are; from 1.0 on the major number is the one that says so, and 1.4
/// and 1.9 are the same contract while 1.4 and 2.0 are not.
/// </para>
/// <para>
/// The suffix describes the build and not the contract, so <c>0.4.0-alpha.1</c> and <c>0.4.0</c> are the same version here.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// GameVersion? declared = VersionCheck.ReadExecutable();
/// VersionMatch match = declared is null ? VersionMatch.Same : VersionCheck.Compare(EngineVersion.Value, declared.Engine);
/// </code>
/// </example>
public static class VersionCheck
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads the declaration of a game from a folder, which is where its executable stands.</summary>
    /// <param name="folder">The folder the game runs from, which holds <c>game.version.json</c>.</param>
    /// <returns>The declaration, or null when the game ships no file and so declares nothing.</returns>
    /// <exception cref="ArgumentException"><paramref name="folder"/> is null, empty or whitespace.</exception>
    /// <exception cref="InvalidDataException">The file is there and holds no declaration, which is a game that says nothing rather than one that says nothing was written for it.</exception>
    /// <remarks>
    /// A file that cannot be read as JSON throws what the reader throws, because a game that ships a broken declaration is a game whose
    /// content is broken. A file that holds <c>null</c> is refused here rather than read as an absent declaration: the two are
    /// different — one is a game that never wrote a file and one is a game that wrote a file saying nothing — and only the first is
    /// the opt-out that a game without a declared contract takes.
    /// </remarks>
    public static GameVersion? Read(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        string path = Path.Combine(folder, GameVersion.FileName);

        if (!File.Exists(path))
        {
            return null;
        }

        using FileStream stream = File.OpenRead(path);

        return JsonSerializer.Deserialize<GameVersion>(stream, Options)
            ?? throw new InvalidDataException(
                $"'{GameVersion.FileName}' holds no declaration. Write the version the game was built for, or remove the file to declare nothing.");
    }

    /// <summary>Reads the declaration of a game from the folder the process runs in.</summary>
    /// <returns>The declaration, or null when the game ships no file and so declares nothing.</returns>
    public static GameVersion? ReadExecutable() => Read(AppContext.BaseDirectory);

    /// <summary>Compares the version of the engine that a game declares with the one that is running it.</summary>
    /// <param name="engine">The version of the engine that is running, such as <see cref="EngineVersion.Value"/>.</param>
    /// <param name="declared">The version of the engine that the game was written for, which it declared itself.</param>
    /// <returns>How the two agree, which is what a game refuses to start over.</returns>
    /// <exception cref="ArgumentException">A version is null, empty or whitespace.</exception>
    /// <remarks>A version of fewer numbers is read as the one with zeros behind it, so a declaration of <c>0.4</c> is read as <c>0.4.0</c>.</remarks>
    public static VersionMatch Compare(string engine, string declared)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engine);
        ArgumentException.ThrowIfNullOrWhiteSpace(declared);

        (int engineMajor, int engineMinor, int enginePatch) = Numbers(engine);
        (int major, int minor, int patch) = Numbers(declared);

        if (engineMajor != major)
        {
            return VersionMatch.Different;
        }

        // Before 1.0 the minor number is what carries a change that a game has to follow, which is what the versioning of this
        // engine says: 0.4 and 0.5 are not the same contract, the way 1.4 and 2.0 are not. From 1.0 on, the major number is the one
        // that says so and a game of another minor or patch of the same major number is run.
        if (engineMajor == 0 && engineMinor != minor)
        {
            return VersionMatch.Different;
        }

        return engineMinor == minor && enginePatch == patch ? VersionMatch.Same : VersionMatch.Compatible;
    }

    /// <summary>Returns the three numbers of a version, reading a version of fewer numbers as the one with zeros behind it.</summary>
    /// <param name="version">The version to read, such as <c>0.4.0</c> or <c>0.4.0-alpha.1</c>.</param>
    /// <returns>The three numbers, which are zero where the version names fewer of them.</returns>
    /// <exception cref="ArgumentException">The version holds more than three numbers, or a number is not a non-negative integer.</exception>
    /// <remarks>
    /// A version that does not read is refused rather than rounded, because two versions that are compared are compared for what they
    /// say: reading <c>0.4.x</c> as <c>0.4.0</c> would run a game of another contract, and a fourth number would be dropped without a
    /// word. A version of fewer than three numbers is one that left the zeros behind it out, which is what <c>0.4</c> is for
    /// <c>0.4.0</c>.
    /// </remarks>
    private static (int Major, int Minor, int Patch) Numbers(string version)
    {
        // A suffix describes the build and not the contract, so it is dropped before the numbers are read: 0.4.0-alpha.1 is 0.4.0.
        int suffix = version.IndexOfAny(['-', '+']);

        if (suffix >= 0)
        {
            version = version[..suffix];
        }

        string[] parts = version.Split('.');

        if (parts.Length > 3)
        {
            throw new ArgumentException(
                $"'{version}' names {parts.Length} numbers, and a version is a major, a minor and a patch number at most.",
                nameof(version));
        }

        return (Number(version, parts, 0), Number(version, parts, 1), Number(version, parts, 2));
    }

    /// <summary>Returns one number of a split version, or zero when the version names fewer than that many of them.</summary>
    /// <param name="version">The whole version, which an error names.</param>
    /// <param name="parts">The numbers the version was split into.</param>
    /// <param name="index">The number to read, counting from zero.</param>
    /// <returns>The number, or zero when the version is shorter than that.</returns>
    /// <exception cref="ArgumentException">The part is present and is not a non-negative integer.</exception>
    /// <remarks>
    /// A part that is there is read strictly: rounding a part that does not read would compare two versions as the same contract when
    /// the game named another one, which is the mistake this whole check exists to catch.
    /// </remarks>
    private static int Number(string version, string[] parts, int index)
    {
        if (index >= parts.Length)
        {
            return 0;
        }

        return int.TryParse(parts[index], out int value) && value >= 0
            ? value
            : throw new ArgumentException($"'{version}' holds '{parts[index]}' where a number is expected.", nameof(version));
    }
}
