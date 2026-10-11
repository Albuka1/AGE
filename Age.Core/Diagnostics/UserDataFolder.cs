namespace Age.Core;

/// <summary>One of the folders that a game writes its own files into, which are the places <see cref="IUserDataService"/> answers.</summary>
public enum UserDataFolder
{
    /// <summary>The settings and the small state of a game, which is what it reads back on the next run.</summary>
    Data,

    /// <summary>The saves of a game, which is what a person comes back to.</summary>
    Saves,

    /// <summary>What a game takes a picture of, which a person keeps or shares.</summary>
    Screenshots,
}
