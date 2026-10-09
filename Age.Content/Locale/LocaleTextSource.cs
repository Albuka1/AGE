using Age.Core;

namespace Age.Content.Locale;

/// <summary>
/// Answers the text of a game from the strings of the language it plays in.
/// </summary>
/// <remarks>
/// This is the seam between the content of a game and the rendering of it: a component names a key, this source asks
/// <see cref="ILocaleService"/> what that key says in the language that is being played, and the rendering draws what comes
/// back without ever holding a string of its own. A key that no language holds is answered with the key itself, counted in
/// <see cref="ILocaleService.Missing"/> and written once in the log, which is what makes a string that is not there visible
/// rather than silent. Its <see cref="Language"/> is the language of the service, so a caller that caches a laid out text
/// knows when to lay it out again.
/// </remarks>
/// <example>
/// <code>
/// services.AddSingleton&lt;ILocaleService&gt;(...);
/// services.AddSingleton&lt;ITextSource&gt;(provider =&gt; new LocaleTextSource(provider.GetRequiredService&lt;ILocaleService&gt;()));
/// </code>
/// </example>
public sealed class LocaleTextSource : ITextSource
{
    private readonly ILocaleService _locale;

    /// <summary>Initializes the source with the strings of a game.</summary>
    /// <param name="locale">The service that holds the languages the game ships.</param>
    /// <exception cref="ArgumentNullException"><paramref name="locale"/> is null.</exception>
    public LocaleTextSource(ILocaleService locale)
    {
        ArgumentNullException.ThrowIfNull(locale);
        _locale = locale;
    }

    /// <inheritdoc />
    public string Language => _locale.Language;

    /// <inheritdoc />
    public string Resolve(string key, params (string Name, object? Value)[] arguments) => _locale.Get(key, arguments);
}
