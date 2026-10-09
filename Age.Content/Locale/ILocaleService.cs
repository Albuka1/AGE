namespace Age.Content.Locale;

/// <summary>
/// Answers a game with the strings of the language it plays in.
/// </summary>
/// <remarks>
/// <para>
/// A game asks for a key rather than holding a text, so a new language is a folder of documents and not a change in code. The
/// strings come from <c>Locale/&lt;language&gt;</c> under the root of the game, which <c>Resources/README.md</c> describes.
/// </para>
/// <para>
/// What the service does with a key that is not there is what the rest of the engine does with an image that is not there: it
/// answers with something visible — the key itself — writes one line in the log for that key however often it is asked, counts
/// it in <see cref="Missing"/>, and leans on a build to fail rather than on a player to notice. A language that the game does
/// not have falls back to <see cref="BaseLanguage"/> first, so a half-written translation shows English rather than keys.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ILocaleService locale = provider.GetRequiredService&lt;ILocaleService&gt;();
///
/// string name = locale.Get("ent-Goblin");                                  // goblin
/// string once = locale.Get("ui-items", ("count", 1));                      // one item
/// string many = locale.Get("ui-items", ("count", 3));                      // three items
/// string desc = locale.Get("ent-Goblin.desc");                             // A small, mean creature.
/// </code>
/// </example>
public interface ILocaleService
{
    /// <summary>Gets the language that is being played, or sets it, which reads the documents of that language.</summary>
    /// <remarks>A language that the game does not have is the base language plus one line in the log rather than an exception.</remarks>
    string Language { get; set; }

    /// <summary>Gets the language that every other language falls back to, which is <c>en</c> and is always there.</summary>
    string BaseLanguage { get; }

    /// <summary>Gets the languages the game ships, which are the folders under <c>Locale</c>, in the order of their names.</summary>
    IEnumerable<string> Languages { get; }

    /// <summary>Gets the keys that were asked for and not found, in the order they were first asked about.</summary>
    IEnumerable<string> Missing { get; }

    /// <summary>Gets the number of strings the language that is being played holds.</summary>
    int Count { get; }

    /// <summary>Returns the string of a key, with the placeholders of its text written out.</summary>
    /// <param name="key">The key to look up, such as <c>ent-Goblin</c> or <c>ent-Goblin.desc</c>.</param>
    /// <param name="arguments">The values of the placeholders of the text, by the name the text writes in the braces.</param>
    /// <returns>The string, or the key itself when the language and the base language both do not hold it.</returns>
    /// <exception cref="ArgumentException">The key is null, empty or whitespace.</exception>
    /// <remarks>A text that writes its string by count picks the form for the argument named <c>count</c>.</remarks>
    string Get(string key, params (string Name, object? Value)[] arguments);

    /// <summary>Returns the string of a key when the language or the base language holds it.</summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="text">Receives the string, with the placeholders of its text written out.</param>
    /// <param name="arguments">The values of the placeholders of the text, by the name the text writes in the braces.</param>
    /// <returns><see langword="true"/> when a language holds the key.</returns>
    /// <exception cref="ArgumentException">The key is null, empty or whitespace.</exception>
    /// <remarks>Asking this way reports nothing: it is what a game calls when a missing key has an answer of its own.</remarks>
    bool TryGet(string key, out string text, params (string Name, object? Value)[] arguments);

    /// <summary>Returns a value indicating whether a language holds a key.</summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="language">The language to look in, or null for every language the game ships.</param>
    /// <returns><see langword="true"/> when a language holds the key.</returns>
    /// <exception cref="ArgumentException">The key is null, empty or whitespace.</exception>
    bool Has(string key, string? language = null);
}
