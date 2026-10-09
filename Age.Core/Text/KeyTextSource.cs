namespace Age.Core;

/// <summary>
/// The source of a game that ships no strings: every key is answered with the key itself.
/// </summary>
/// <remarks>
/// It is what a game that holds its text in code shows, and what a component names before a game registered a source of its
/// own, so text never disappears from a frame: a key that is drawn as it is written is a mistake that is easy to see. The
/// source holds no state, so one instance serves a whole game.
/// </remarks>
public sealed class KeyTextSource : ITextSource
{
    /// <summary>Gets the one source of this kind.</summary>
    public static KeyTextSource Instance { get; } = new();

    /// <inheritdoc />
    /// <remarks>No language is being played, so the key itself is what every text says.</remarks>
    public string Language => string.Empty;

    /// <inheritdoc />
    public string Resolve(string key, params (string Name, object? Value)[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return key;
    }
}
