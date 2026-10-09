using System.Globalization;
using System.Text;
using Age.Assets;
using Microsoft.Extensions.Logging;

namespace Age.Content.Locale;

/// <summary>
/// The default <see cref="ILocaleService"/>: it reads the documents of a language through <see cref="IAssetLoader"/> and keeps
/// what they say by key.
/// </summary>
/// <remarks>
/// A language is read once, when it is first asked for, and everything a document refers to is followed while it is read: what
/// a game asks for afterwards is a lookup. A key that is not there is answered with the key itself, counted and written once in
/// the log, which is what the engine does with an image that is not there as well.
/// </remarks>
public sealed class LocaleService : ILocaleService
{
    /// <summary>The folder under the root of the game that holds the languages, one folder each.</summary>
    public const string Folder = "Locale";

    /// <summary>The language that every other language falls back to, which a game that adds a language of its own changes.</summary>
    public const string Base = "en";

    private readonly IAssetLoader _assets;
    private readonly ILogger<LocaleService>? _logger;
    private readonly CultureInfo? _culture;
    private readonly Dictionary<string, LocaleLanguage> _languages = new(StringComparer.Ordinal);
    private readonly List<string> _missing = [];
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);
    private string _language = Base;

    /// <summary>Initializes the service with the files of the game.</summary>
    /// <param name="assets">The loader that reads the documents.</param>
    /// <param name="logger">The logger that reports what is missing, or null to report nothing.</param>
    /// <param name="culture">The culture that decides what the game plays in, or null for the one the system is set to.</param>
    /// <exception cref="ArgumentNullException">The loader is null.</exception>
    /// <remarks>
    /// The language that is being played starts as <see cref="SystemLanguage"/>, which is the language the system is set to
    /// when the game ships it and the base language otherwise, so a game shows its own language without anyone choosing one.
    /// </remarks>
    public LocaleService(IAssetLoader assets, ILogger<LocaleService>? logger = null, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(assets);
        _assets = assets;
        _logger = logger;
        _culture = culture;

        // The base language is read at once rather than at the first frame: a game that ships a document which cannot be read
        // hears about it while it is starting, and a test that reads the strings of the engine does not have to ask first.
        Load(Base);

        // The system decides what a game plays in, and only when the game ships it: a game that ships English and Russian
        // starts in Russian on a machine that is set to Russian, and in English anywhere else.
        _language = SystemLanguage;
        Load(_language);
    }

    /// <inheritdoc />
    public string BaseLanguage => Base;

    /// <inheritdoc />
    public string SystemLanguage
    {
        get
        {
            CultureInfo culture = _culture ?? CultureInfo.CurrentUICulture;

            // The name of a culture is the whole of it, such as 'pt-BR', which is what a game that ships one translation of a
            // language names the folder of it; the two letters are what a game that ships every region of it names the folder.
            return Shipped(culture.Name) ?? Shipped(culture.TwoLetterISOLanguageName) ?? Base;
        }
    }

    /// <summary>Returns the language of the game with the given name, or null when the game ships no language of that name.</summary>
    /// <param name="name">The name of a language, such as <c>ru</c> or <c>pt-BR</c>.</param>
    /// <returns>The folder of the language the way the game writes it, or null when the game holds no such language.</returns>
    private string? Shipped(string name) =>
        Languages.FirstOrDefault(language => string.Equals(language, name, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    /// <exception cref="ArgumentException">The language is null, empty or whitespace.</exception>
    public string Language
    {
        get => _language;

        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _language = value;

            // Read now rather than in the middle of a frame: a language that the game does not have says so once, here.
            Load(value);
        }
    }

    /// <inheritdoc />
    public IEnumerable<string> Languages
    {
        get
        {
            IEnumerable<string> files;

            try
            {
                files = _assets.Enumerate(Folder);
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException)
            {
                // A game that ships no strings at all has no folder of languages, which is not a mistake: it is a game whose
                // content does not say anything yet.
                return [];
            }

            return [.. files
                .Select(file => file.Split('/')[1])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(language => language, StringComparer.Ordinal)];
        }
    }

    /// <inheritdoc />
    public IEnumerable<string> Missing => _missing;

    /// <inheritdoc />
    public int Count => Load(_language).Count;

    /// <inheritdoc />
    public string Get(string key, params (string Name, object? Value)[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (Read(_language, key, arguments) is string text)
        {
            return text;
        }

        if (!string.Equals(_language, Base, StringComparison.Ordinal) && Read(Base, key, arguments) is string fallback)
        {
            return fallback;
        }

        Report(key, $"The string '{key}' is not one that any language of the game holds, so the key itself is drawn.");

        return key;
    }

    /// <inheritdoc />
    public bool TryGet(string key, out string text, params (string Name, object? Value)[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        text = Read(_language, key, arguments)
            ?? (!string.Equals(_language, Base, StringComparison.Ordinal) ? Read(Base, key, arguments) : null)
            ?? key;

        return !string.Equals(text, key, StringComparison.Ordinal) || Has(key);
    }

    /// <inheritdoc />
    public bool Has(string key, string? language = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (language is not null)
        {
            return Holds(Load(language), key);
        }

        return Holds(Load(_language), key) || Holds(Load(Base), key);
    }

    /// <summary>Determines whether a language holds a key, which may name what a key says besides its text.</summary>
    private static bool Holds(LocaleLanguage language, string key)
    {
        if (language.TryGet(key, out LocaleString? _))
        {
            return true;
        }

        int dot = key.LastIndexOf('.');

        if (dot <= 0 || !language.TryGet(key[..dot], out LocaleString? owner))
        {
            return false;
        }

        string part = key[(dot + 1)..];

        return owner.Attributes.ContainsKey(part) || owner.Forms.ContainsKey(part);
    }

    /// <inheritdoc />
    public override string ToString() => $"the strings of the game in '{_language}', {Count} of them";

    /// <summary>Returns the strings of a language, reading the documents of it the first time it is asked for.</summary>
    private LocaleLanguage Load(string language)
    {
        if (_languages.TryGetValue(language, out LocaleLanguage? loaded))
        {
            return loaded;
        }

        if (!Languages.Contains(language, StringComparer.Ordinal))
        {
            // A language the game does not have is answered by the base language, and a game that holds no strings at all is a
            // game whose content says nothing yet rather than a mistake. Only a language that the game was asked for and does
            // not hold is worth a line, and the folder is not walked here, so nothing says the same thing twice.
            if (!string.Equals(language, Base, StringComparison.Ordinal))
            {
                Report(language, $"The game holds no strings for '{language}', so the strings of '{Base}' are the ones that answer. Its languages are {string.Join(", ", Languages)}.");
            }

            loaded = new LocaleLanguage(language, new Dictionary<string, LocaleString>(StringComparer.Ordinal));
            _languages[language] = loaded;

            return loaded;
        }

        var strings = new Dictionary<string, LocaleString>(StringComparer.Ordinal);
        var failed = false;

        try
        {
            foreach (string file in _assets.Enumerate($"{Folder}/{language}"))
            {
                if (!IsDocument(file))
                {
                    continue;
                }

                foreach ((string key, LocaleString value) in LocaleReader.Read(_assets.Load<string>(file), file))
                {
                    // A key that two documents of one language both write is a mistake of the content, which a build of it
                    // refuses: here the first one is kept and said once, because a frame is not the place to stop a game.
                    if (!strings.TryAdd(key, value))
                    {
                        Report($"{language}:{key}", $"The key '{key}' is written by more than one document of '{language}', and the first one is the one that is played.");
                    }
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
            // The folder went away between the listing of the languages and the reading of it, which leaves an empty language
            // that is kept: a game's content does not come and go while it runs.
        }
        catch (Exception exception) when (exception is LocaleException or IOException or InvalidOperationException or ArgumentException)
        {
            // A language whose documents cannot be read is not kept: what is reported here is content that has to change, and a
            // caller that asks again reads the documents again rather than living with the half of a language.
            failed = true;
            Report(language, $"The strings of '{language}' cannot be read: {exception.Message}");
        }

        loaded = new LocaleLanguage(language, Resolve(language, strings));

        if (!failed)
        {
            _languages[language] = loaded;
        }

        return loaded;
    }

    /// <summary>Follows every reference of a language, so that answering with a string is a lookup rather than a walk.</summary>
    private Dictionary<string, LocaleString> Resolve(string language, Dictionary<string, LocaleString> strings)
    {
        var resolved = new Dictionary<string, LocaleString>(strings.Count, StringComparer.Ordinal);

        foreach ((string key, LocaleString value) in strings)
        {
            resolved[key] = value with
            {
                Text = value.Text is null ? null : Reference(language, strings, value.Text, []),
                Attributes = Parts(language, strings, value.Attributes),
                Forms = Parts(language, strings, value.Forms),
            };
        }

        return resolved;
    }

    /// <summary>Follows the references of the attributes or the forms of a string.</summary>
    private Dictionary<string, string> Parts(string language, Dictionary<string, LocaleString> strings, IReadOnlyDictionary<string, string> parts)
    {
        var followed = new Dictionary<string, string>(parts.Count, StringComparer.Ordinal);

        foreach ((string name, string text) in parts)
        {
            followed[name] = Reference(language, strings, text, []);
        }

        return followed;
    }

    /// <summary>Returns what a text says, following it when it is a reference to another key or to one of its attributes.</summary>
    private string Reference(string language, Dictionary<string, LocaleString> strings, string text, HashSet<string> seen)
    {
        if (LocaleReference.TryKey(text) is not string reference)
        {
            return text;
        }

        // A reference that cannot be followed is not a game that stops: what is drawn is the name of what it pointed at, which
        // is what a person reads and fixes, and the log hears about it once.
        if (!seen.Add(reference))
        {
            Report($"{language}:{reference}", $"The string '{reference}' of '{language}' refers to itself, and a string that is a circle says nothing.");
            return reference;
        }

        string? found = null;

        if (strings.TryGetValue(reference, out LocaleString? whole))
        {
            found = whole.Text;
        }
        else
        {
            int dot = reference.LastIndexOf('.');

            if (dot > 0
                && strings.TryGetValue(reference[..dot], out LocaleString? owner)
                && owner.Attributes.TryGetValue(reference[(dot + 1)..], out string? attribute))
            {
                found = attribute;
            }
        }

        if (found is null)
        {
            Report($"{language}:{reference}", $"The string '{reference}' of '{language}' is referred to by another one, and the language does not hold it.");
            return reference;
        }

        return Reference(language, strings, found, seen);
    }

    /// <summary>Returns the string of a key in one language, with its placeholders written, or null when that language has no such key.</summary>
    private string? Read(string language, string key, (string Name, object? Value)[] arguments)
    {
        LocaleLanguage loaded = Load(language);

        if (!loaded.TryGet(key, out LocaleString? value))
        {
            // A key that a document writes with a dot, such as 'ent-Goblin.desc', is what the key before the dot says besides
            // its text: the caller names the whole of it, and what it gets is that part.
            int dot = key.LastIndexOf('.');

            if (dot <= 0 || !loaded.TryGet(key[..dot], out LocaleString? owner))
            {
                return null;
            }

            string part = key[(dot + 1)..];

            string? found = owner.Attributes.TryGetValue(part, out string? attribute)
                ? attribute
                : owner.Forms.TryGetValue(part, out string? form) ? form : null;

            return found is null ? null : Format(language, key, found, arguments);
        }

        string? whole = value.HasForms ? Form(language, key, value, arguments) : value.Text;

        return whole is null ? null : Format(language, key, whole, arguments);
    }

    /// <summary>Returns the form of a string that is written by count, which is the one its language selects.</summary>
    private string? Form(string language, string key, LocaleString value, (string Name, object? Value)[] arguments)
    {
        var count = 0L;

        foreach ((string name, object? argument) in arguments)
        {
            // The form is selected by the argument named 'count', which is what a text that writes a count is asked for with.
            if (string.Equals(name, "count", StringComparison.Ordinal) && argument is not null)
            {
                try
                {
                    count = Convert.ToInt64(argument, CultureInfo.InvariantCulture);
                }
                catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
                {
                    // A count that is not a number is a mistake of the call rather than of the content: the form of 'other' is
                    // what such a call gets, and the mistake is not worth stopping a game for.
                    count = 0;
                }

                break;
            }
        }

        string category = PluralRules.Name(PluralRules.Category(language, count));

        if (value.Forms.TryGetValue(category, out string? form))
        {
            return form;
        }

        // Every language that writes a count has the form of 'other', which is what the counts it does not name fall to.
        if (value.Forms.TryGetValue(PluralRules.Name(PluralCategory.Other), out string? other))
        {
            Report($"{language}:{key}:{category}", $"The string '{key}' of '{language}' has no form '{category}', so its form 'other' is drawn.");
            return other;
        }

        Report($"{language}:{key}", $"The string '{key}' of '{language}' is written by count and has neither the form '{category}' nor the form 'other'.");

        return null;
    }

    /// <summary>Writes the placeholders of a text out, which is what turns a key and its arguments into a line a person reads.</summary>
    private string Format(string language, string key, string text, (string Name, object? Value)[] arguments)
    {
        if (!text.Contains('{', StringComparison.Ordinal))
        {
            return text;
        }

        var written = new StringBuilder(text.Length);

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '{')
            {
                written.Append(text[index]);
                continue;
            }

            int end = text.IndexOf('}', index + 1);

            if (end < 0)
            {
                written.Append(text[index..]);
                break;
            }

            string name = text[(index + 1)..end].Trim();

            if (Value(name, arguments) is string value)
            {
                written.Append(value);
            }
            else
            {
                // A placeholder that nothing fills is a mistake of the call rather than of the content, so the name stays
                // visible in the line and is said once: what a person reads is the name they forgot to pass.
                written.Append(text[index..(end + 1)]);
                Report($"{language}:{key}:{name}", $"The string '{key}' writes the placeholder '{{{name}}}' and nothing was passed for it.");
            }

            index = end;
        }

        return written.ToString();
    }

    /// <summary>Returns the value of a placeholder by name, or null when the call passed nothing for it.</summary>
    private static string? Value(string name, (string Name, object? Value)[] arguments)
    {
        foreach ((string declared, object? value) in arguments)
        {
            if (string.Equals(declared, name, StringComparison.Ordinal) && value is not null)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    /// <summary>Leaves one record and one line in the log for what the game does not hold, whatever asks for it again.</summary>
    private void Report(string what, string message)
    {
        if (_reported.Add(what))
        {
            _missing.Add(what);
            _logger?.LogError("{Reason}", message);
        }
    }

    /// <summary>Determines whether a file is a document of YAML, which is what a language is written in.</summary>
    private static bool IsDocument(string file) =>
        file.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase);
}
