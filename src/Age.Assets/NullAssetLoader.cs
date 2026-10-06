namespace Age.Assets;

/// <summary>
/// The default asset loader. It resolves paths inside a sandbox rooted at the initialized game root and rejects any path that escapes it.
/// </summary>
public sealed class NullAssetLoader : IAssetLoader
{
    private string? _root;

    /// <inheritdoc />
    public void Initialize(string gameRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameRoot);
        _root = Path.GetFullPath(gameRoot);
    }

    /// <inheritdoc />
    public bool Exists(string relativePath) => File.Exists(Resolve(relativePath));

    /// <inheritdoc />
    public Stream OpenRead(string relativePath) => File.OpenRead(Resolve(relativePath));

    private string Resolve(string relativePath)
    {
        string root = _root ?? throw new InvalidOperationException("The asset loader has not been initialized. Call Initialize first.");

        if (string.IsNullOrEmpty(relativePath))
        {
            throw new InvalidOperationException("The path must not be empty.");
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException($"Absolute paths are not allowed: '{relativePath}'.");
        }

        string combined = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!IsInside(root, combined))
        {
            throw new InvalidOperationException($"The path escapes the game root: '{relativePath}'.");
        }

        EnsureLinksStayInside(root, combined, relativePath);
        return combined;
    }

    private static void EnsureLinksStayInside(string root, string path, string relativePath)
    {
        string current = path;
        while (current.Length >= root.Length)
        {
            FileSystemInfo? target = Directory.Exists(current)
                ? Directory.ResolveLinkTarget(current, returnFinalTarget: true)
                : File.Exists(current)
                    ? File.ResolveLinkTarget(current, returnFinalTarget: true)
                    : null;

            if (target is not null && !IsInside(root, Path.GetFullPath(target.FullName)))
            {
                throw new InvalidOperationException($"The path resolves outside the game root: '{relativePath}'.");
            }

            current = Path.GetDirectoryName(current) ?? string.Empty;
        }
    }

    private static bool IsInside(string root, string path) =>
        string.Equals(path, root, StringComparison.Ordinal) ||
        path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
}
