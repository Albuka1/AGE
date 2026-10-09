using System.Diagnostics.CodeAnalysis;

namespace Age.Content.Locale;

/// <summary>
/// The strings of one language, read and resolved by key.
/// </summary>
/// <remarks>
/// A language is read once and kept: every reference it makes is followed while it is read, so answering with a string is a
/// lookup rather than a walk. The strings are what the documents wrote, and the service that answers a game is what falls back
/// to another language, reports what is missing and writes the placeholders.
/// </remarks>
public sealed class LocaleLanguage
{
    /// <summary>Initializes the strings of one language.</summary>
    /// <param name="name">The name of the language, which is the folder it was read from.</param>
    /// <param name="strings">The strings by key.</param>
    internal LocaleLanguage(string name, IReadOnlyDictionary<string, LocaleString> strings)
    {
        Name = name;
        Strings = strings;
    }

    /// <summary>Gets the name of the language, such as <c>en</c>.</summary>
    public string Name { get; }

    /// <summary>Gets the number of strings the language holds.</summary>
    public int Count => Strings.Count;

    /// <summary>Gets the keys of the language.</summary>
    public IEnumerable<string> Keys => Strings.Keys;

    /// <summary>The strings by key, which the service reads and a game does not.</summary>
    internal IReadOnlyDictionary<string, LocaleString> Strings { get; }

    /// <summary>Returns a string of the language by key.</summary>
    /// <param name="key">The key to look up, which is matched exactly.</param>
    /// <param name="value">Receives the string when the language declares the key.</param>
    /// <returns><see langword="true"/> when the language declares the key.</returns>
    public bool TryGet(string key, [NotNullWhen(true)] out LocaleString? value) => Strings.TryGetValue(key, out value);

    /// <inheritdoc />
    public override string ToString() => $"the language '{Name}' with {Count} strings";
}
