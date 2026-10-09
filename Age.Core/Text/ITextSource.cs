namespace Age.Core;

/// <summary>
/// Answers a game with the string of a key, in the language it plays in.
/// </summary>
/// <remarks>
/// <para>
/// The text that a game shows is content rather than a string of its code: a component names a key and the source answers
/// what that key says, so a new language is a document of strings and not a change in the game. This is the seam that lets
/// the rendering hold text without knowing where the strings come from: <c>Age.Content</c> implements it over the locale of
/// a game, and the null implementation answers every key with the key itself, which is what a game without content shows.
/// </para>
/// <para>
/// A source is not thread-safe unless it says so, and a caller that caches a string asks again once
/// <see cref="Language"/> changed, because that is the one thing that makes the same key say something else.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ITextSource text = provider.GetRequiredService&lt;ITextSource&gt;();
///
/// string items = text.Resolve("ui-items", ("count", 3));      // three items
/// string name = text.Resolve("ent-Goblin");                   // a goblin
/// </code>
/// </example>
public interface ITextSource
{
    /// <summary>Gets the language that the strings are answered in, which changes when a game switches language.</summary>
    string Language { get; }

    /// <summary>Returns the string of a key, with the placeholders of its text written out.</summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="arguments">The values of the placeholders of the text, by the name the text writes in the braces.</param>
    /// <returns>The string, or the key itself when no language of the game holds it.</returns>
    /// <exception cref="ArgumentException">The key is null, empty or whitespace.</exception>
    /// <remarks>A text that writes its string by count picks the form for the argument named <c>count</c>.</remarks>
    string Resolve(string key, params (string Name, object? Value)[] arguments);
}
