namespace Age.Assets;

/// <summary>
/// Reads game content by path relative to the game root. Path-based access only; Load&lt;T&gt; is planned.
/// </summary>
public interface IAssetLoader
{
    /// <summary>Sets the game root that every relative path is resolved against. This is the only entry point.</summary>
    void Initialize(string gameRoot);

    /// <summary>Determines whether a file exists at the given path relative to the game root.</summary>
    bool Exists(string relativePath);

    /// <summary>Opens a readable stream for the file at the given path relative to the game root.</summary>
    Stream OpenRead(string relativePath);
}
