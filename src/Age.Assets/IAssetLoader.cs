namespace Age.Assets;

/// <summary>
/// Reads game content by path relative to the game root. Every path is sandboxed inside that root.
/// </summary>
public interface IAssetLoader
{
    /// <summary>Sets the game root that every relative path is resolved against. This is the only entry point.</summary>
    void Initialize(string gameRoot);

    /// <summary>Determines whether a file exists at the given path relative to the game root.</summary>
    bool Exists(string relativePath);

    /// <summary>Opens a readable stream for the file at the given path relative to the game root.</summary>
    Stream OpenRead(string relativePath);

    /// <summary>
    /// Reads the file at the given path relative to the game root. <c>string</c> reads UTF-8 text and <c>byte[]</c> reads
    /// the raw bytes; every other type is deserialized from JSON, including the engine components whose data lives in
    /// public fields. JSON allows comments, trailing commas and case-insensitive property names.
    /// </summary>
    T Load<T>(string relativePath);
}
