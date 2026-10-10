namespace Age.Assets;

/// <summary>
/// Reads game content by path relative to the game root. Every path is sandboxed inside that root, so a path that
/// escapes it throws.
/// </summary>
/// <remarks>
/// The generic <see cref="Load{T}(string)"/> deserializes JSON with reflection, which an AOT build trims away. Call
/// <see cref="Load{T}(string, System.Text.Json.Serialization.Metadata.JsonTypeInfo{T})"/> with a contract from a source
/// generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> in a build like that; the engine does
/// the same for scenes.
/// </remarks>
/// <example>
/// <code>
/// assets.Initialize("Resources");
///
/// TransformComponent spawn = assets.Load&lt;TransformComponent&gt;("spawn.json");
/// string readme = assets.Load&lt;string&gt;("readme.txt");
/// byte[] blob = assets.Load&lt;byte[]&gt;("logo.bin");
/// </code>
/// </example>
public interface IAssetLoader
{
    /// <summary>Sets the game root that every relative path is resolved against. This is the only entry point.</summary>
    /// <param name="gameRoot">The directory that holds the game content.</param>
    /// <exception cref="ArgumentException">The root is null, empty or whitespace.</exception>
    void Initialize(string gameRoot);

    /// <summary>Gets the folder that the loader was initialized with, or null when it was not initialized yet.</summary>
    /// <remarks>
    /// A tool that is handed the folders of a game wants to know where the loader reads them from, so that a root of its own and a root of the loader are not two different games: <c>Age.Content.Lint</c> answers with this before it reads anything.
    /// </remarks>
    string? Root { get; }

    /// <summary>Determines whether a file exists at the given path relative to the game root.</summary>
    /// <param name="relativePath">The path of the file, relative to the game root.</param>
    /// <returns><see langword="true"/> when a file exists at that path.</returns>
    /// <exception cref="InvalidOperationException">The loader has not been initialized.</exception>
    bool Exists(string relativePath);

    /// <summary>Opens a readable stream for the file at the given path relative to the game root.</summary>
    /// <param name="relativePath">The path of the file, relative to the game root.</param>
    /// <returns>An open stream that the caller owns and disposes.</returns>
    /// <exception cref="InvalidOperationException">The loader has not been initialized.</exception>
    /// <exception cref="FileNotFoundException">No file exists at that path.</exception>
    Stream OpenRead(string relativePath);

    /// <summary>Returns the files that a folder holds, in it and below it, as paths relative to the game root.</summary>
    /// <param name="relativeFolder">The folder, relative to the game root.</param>
    /// <returns>The paths of the files, relative to the game root, ordered by ordinal.</returns>
    /// <exception cref="ArgumentException">The path is null, empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The loader has not been initialized, or the path escapes the game root.</exception>
    /// <exception cref="DirectoryNotFoundException">No folder exists at that path.</exception>
    /// <remarks>
    /// The paths are ordered, so a caller that reads every file of a folder reads them in the same order on every machine:
    /// content that declares what another file declares would otherwise be read in whichever order the file system
    /// happened to return it. A path is written with a forward slash, whatever the platform uses between directories.
    /// </remarks>
    IEnumerable<string> Enumerate(string relativeFolder);

    /// <summary>Reads the file at the given path relative to the game root and converts it to <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The type to produce. <c>string</c> and <c>byte[]</c> are handled directly, every other type is deserialized from JSON.</typeparam>
    /// <param name="relativePath">The path of the file, relative to the game root.</param>
    /// <returns>The content of the file as a <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">The loader has not been initialized, or the file holds a JSON null.</exception>
    /// <exception cref="FileNotFoundException">No file exists at that path.</exception>
    /// <exception cref="T:System.Text.Json.JsonException">The file does not hold valid JSON.</exception>
    /// <remarks>
    /// A <c>string</c> is read as UTF-8 text and a <c>byte[]</c> receives the raw bytes of the file. Any other type is
    /// deserialized from JSON, which tolerates comments, trailing commas and differently cased names, and binds fields
    /// as well as properties so that the engine components, whose data lives in public fields, work directly.
    /// </remarks>
    T Load<T>(string relativePath);

    /// <summary>Reads the JSON file at the given path relative to the game root through a source generated contract.</summary>
    /// <typeparam name="T">The type to produce.</typeparam>
    /// <param name="relativePath">The path of the file, relative to the game root.</param>
    /// <param name="typeInfo">The contract of <typeparamref name="T"/>, taken from a source generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>.</param>
    /// <returns>The content of the file as a <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">The loader has not been initialized, or the file holds a JSON null.</exception>
    /// <exception cref="FileNotFoundException">No file exists at that path.</exception>
    /// <exception cref="T:System.Text.Json.JsonException">The file does not hold valid JSON for the contract.</exception>
    /// <remarks>
    /// This overload never uses reflection, so it keeps working in a build that trims or compiles ahead of time. It
    /// always reads JSON; a <c>string</c> or a <c>byte[]</c> is read with the overload above.
    /// </remarks>
    T Load<T>(string relativePath, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo);
}
