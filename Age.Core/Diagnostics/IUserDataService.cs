namespace Age.Core;

/// <summary>
/// The folder of a game that belongs to the person playing it: where its settings, its saves and whatever else it writes while it
/// runs are kept, away from the folder the game was installed into.
/// </summary>
/// <remarks>
/// <para>
/// A game is installed read-only, and the account that runs it may not be allowed to write beside the executable at all. What a
/// person changes while playing, on the other hand, is theirs and outlives one installation: a setting, a save and a screenshot
/// belong to them rather than to the build, so they live under the roaming application data of the account, in a folder named
/// after the game.
/// </para>
/// <para>
/// The folders are created on the first ask rather than up front, so a game that writes nothing leaves nothing behind, and the
/// folders of this service are the only places a game writes its own files. A game that writes a screenshot into
/// <see cref="Screenshots"/>, a save into <see cref="Saves"/> and its settings into <see cref="Data"/> is found again on the next
/// run and on every machine, because the folder is derived from the name of the game rather than from where it was installed.
/// </para>
/// <para>
/// The layout under the roaming folder of the account is <c>{game}/data</c>, and a game names a subfolder of it when it asks for
/// one. Nothing is written by the service itself: it answers the paths and creates the folders, and the game writes through the
/// file system the way it would anywhere else.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// string settings = data.PathIn(UserDataFolder.Data, "settings.json");
/// string save = data.PathIn(UserDataFolder.Saves, "campaign.json");
/// string shot = data.PathIn(UserDataFolder.Screenshots, "shot.png");
///
/// File.WriteAllText(settings, json);
/// </code>
/// </example>
public interface IUserDataService
{
    /// <summary>Gets the name of the game, which is the folder of its data under the roaming application data of the account.</summary>
    string Game { get; }

    /// <summary>Gets the root of the data of this game, which is <c>%APPDATA%/{game}</c> on Windows and the matching folder of the account elsewhere.</summary>
    /// <remarks>The root is the folder that the other folders below are named under, and it is created with them on the first ask.</remarks>
    string Root { get; }

    /// <summary>Gets the folder that holds the settings and the small state of a game, which is <c>{root}/data</c>.</summary>
    string Data { get; }

    /// <summary>Gets the folder that holds the saves of a game, which is <c>{root}/data/saves</c>.</summary>
    string Saves { get; }

    /// <summary>Gets the folder that holds what a game takes a picture of, which is <c>{root}/data/screenshots</c>.</summary>
    string Screenshots { get; }

    /// <summary>Returns the folder of one of the kinds of data of this game, creating it and the folders above it when they are not there.</summary>
    /// <param name="folder">The kind of data, which is one of the folders above.</param>
    /// <returns>The full path of the folder, which is there when the call answers.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The kind of data is not one this service knows.</exception>
    /// <remarks>A game that writes nothing pays nothing: the folders are made here and nowhere else, so a first run that writes no file leaves no folder behind.</remarks>
    string Ensure(UserDataFolder folder);

    /// <summary>Returns the path of a file in one of the folders of this game, whose folder is created when it is not there.</summary>
    /// <param name="folder">The kind of data the file belongs to.</param>
    /// <param name="name">The name of the file, which may hold a subfolder.</param>
    /// <returns>The full path of the file. The file itself is not created.</returns>
    /// <exception cref="ArgumentNullException">The name is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The kind of data is not one this service knows.</exception>
    /// <remarks>
    /// The name is a path below the folder, so a game groups its saves by the character they belong to without asking for a folder
    /// of its own. A name that climbs out of the folder with <c>..</c> is refused, which keeps a name of a file a name rather than a
    /// path to anywhere on the disk.
    /// </remarks>
    string PathIn(UserDataFolder folder, string name);
}
