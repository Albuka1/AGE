namespace Age.Assets;

/// <summary>
/// Reads game content by path relative to the game root. Every path is sandboxed inside that root, so a path that
/// escapes it throws.
/// </summary>
/// <example>
/// <code>
/// assets.Initialize("content");
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
}
