namespace Age.Core;

/// <summary>
/// The default <see cref="IUserDataService"/>: the folders under the roaming application data of the account, named after the game.
/// </summary>
/// <remarks>
/// The roaming folder is the one every platform keeps an application's own files in, and the engine reaches it through
/// <see cref="Environment.SpecialFolder.ApplicationData"/>, which is the roaming folder on Windows and the matching folder on a
/// machine that keeps one somewhere else. A machine that answers nothing falls back to the local application data of the account,
/// so a game writes somewhere it may write rather than beside the executable it may not.
/// </remarks>
/// <example>
/// <code>
/// services.AddAgeCore();
/// services.AddSingleton&lt;IUserDataService&gt;(provider =&gt; new UserDataService("Age", provider.GetService&lt;ILogger&lt;UserDataService&gt;&gt;()));
/// </code>
/// </example>
public sealed class UserDataService : IUserDataService
{
    /// <summary>The folder under the root of the data of a game that holds everything this service answers.</summary>
    public const string DataFolder = "data";

    private readonly string _root;

    /// <summary>Initializes the service with the name of the game and the folder the account keeps its application data in.</summary>
    /// <param name="game">The name of the game, which is the folder of its data. It must not be empty or hold a path separator.</param>
    /// <param name="roaming">The folder the account keeps its application data in, or null to ask the platform for it.</param>
    /// <exception cref="ArgumentException">The name is null, empty, whitespace, or holds a character that a folder name cannot.</exception>
    /// <remarks>
    /// The folder of the account is asked for here rather than on every call, so the path of a folder is a lookup by the time a game
    /// asks for it: the four paths of a game are the same for the whole run, whatever the platform does while it runs.
    /// </remarks>
    public UserDataService(string game, string? roaming = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(game);

        if (game.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || game.Contains(Path.DirectorySeparatorChar) || game.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("The name of the game is a folder name, so it holds no path separator and no character a folder cannot.", nameof(game));
        }

        Game = game;
        _root = Path.Combine(ApplicationData(roaming), game);
    }

    /// <inheritdoc />
    public string Game { get; }

    /// <inheritdoc />
    public string Root => _root;

    /// <inheritdoc />
    public string Data => Path.Combine(Root, DataFolder);

    /// <inheritdoc />
    public string Saves => Path.Combine(Data, "saves");

    /// <inheritdoc />
    public string Screenshots => Path.Combine(Data, "screenshots");

    /// <inheritdoc />
    public string Ensure(UserDataFolder folder)
    {
        string path = Folder(folder);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <inheritdoc />
    public string PathIn(UserDataFolder folder, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        string path = Path.GetFullPath(Path.Combine(Ensure(folder), name));

        // The folder is normalized the same way the name is, so the two are compared in the one absolute form: a folder that was
        // given as a relative path, or one with a trailing separator, would otherwise never match a path that was joined to it.
        string folderPath = Path.GetFullPath(Folder(folder));

        // The name is a path below the folder of this game and nowhere else: a name that climbs out of it is refused rather than
        // followed, which is what keeps a name of a file a name rather than a way to write anywhere on the disk.
        if (!path.StartsWith(folderPath + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !string.Equals(path, folderPath, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The name '{name}' points outside the folder of the game.", nameof(name));
        }

        return path;
    }

    /// <summary>Returns the path of one of the folders of this game, without creating it.</summary>
    /// <param name="folder">The kind of data.</param>
    /// <exception cref="ArgumentOutOfRangeException">The kind of data is not one this service knows.</exception>
    private string Folder(UserDataFolder folder) => folder switch
    {
        UserDataFolder.Data => Data,
        UserDataFolder.Saves => Saves,
        UserDataFolder.Screenshots => Screenshots,
        _ => throw new ArgumentOutOfRangeException(nameof(folder), folder, "There is no folder of the data of a game by that name."),
    };

    /// <summary>Returns the folder the account keeps its application data in, asking the platform when one was not given.</summary>
    /// <param name="roaming">The folder to use, or null to ask the platform for it.</param>
    /// <remarks>
    /// The roaming folder is what a Windows account carries from one machine to another, and it is the folder a game is expected to
    /// write its own files into. A platform that answers nothing is a platform that keeps no such folder, and the local application
    /// data of the account is where a game writes then, which is still a folder it may write rather than the one it was installed in.
    /// </remarks>
    private static string ApplicationData(string? roaming)
    {
        if (!string.IsNullOrWhiteSpace(roaming))
        {
            return roaming;
        }

        string folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        return string.IsNullOrWhiteSpace(folder)
            ? Path.Combine(Path.GetTempPath(), "Age")
            : folder;
    }
}
