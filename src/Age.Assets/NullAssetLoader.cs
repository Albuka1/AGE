using System.Text;
using System.Text.Json;

namespace Age.Assets;

/// <summary>
/// The default <see cref="IAssetLoader"/>, registered by <c>AddAgeAssets</c> and used when no other loader is
/// registered. It resolves paths inside a sandbox rooted at the initialized game root, rejects any path that escapes
/// it, and reads UTF-8 text, raw bytes and JSON.
/// </summary>
/// <remarks>
/// The sandbox refuses absolute paths, paths that climb out of the root with <c>..</c> and links whose resolved target
/// leaves the root, so a game cannot reach arbitrary files by choosing an asset path.
/// </remarks>
public sealed class NullAssetLoader : IAssetLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        IncludeFields = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

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

    /// <inheritdoc />
    public T Load<T>(string relativePath)
    {
        using Stream stream = OpenRead(relativePath);

        if (typeof(T) == typeof(string))
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return (T)(object)reader.ReadToEnd();
        }

        if (typeof(T) == typeof(byte[]))
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return (T)(object)buffer.ToArray();
        }

        T? value = JsonSerializer.Deserialize<T>(stream, JsonOptions);
        if (value is null)
        {
            throw new InvalidOperationException($"The asset '{relativePath}' does not contain a {typeof(T).Name}.");
        }

        return value;
    }

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
