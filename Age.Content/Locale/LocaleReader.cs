using Age.Content.Yaml;

namespace Age.Content.Locale;

/// <summary>
/// Reads the document that holds the strings of one language, which lives under the folder of that language.
/// </summary>
/// <remarks>
/// <para>
/// The document is written in the subset of YAML that the rest of the content uses, and it is a mapping of keys:
/// </para>
/// <example>
/// <code>
/// ent-Goblin: goblin
/// ent-Goblin.desc: A small, mean creature.
/// ent-GoblinHeavy: "{ ent-Goblin }"
/// ent-GoblinHeavy.desc: "{ ent-Goblin.desc }"
/// ui-items: "{count} items"
/// ui-items.one: "{count} item"
/// </code>
/// </example>
/// <para>
/// A key that holds a dot is what the key before the dot says <em>besides</em> its text: <c>one</c>, <c>few</c> and the other
/// names of forms say what the message writes for a count, and any other word is an attribute, such as the <c>desc</c> of an
/// entity. A key either writes its text or the forms of it, not both, and a text that is a reference — <c>{ ent-Base }</c> — is
/// left as it was written: the service that reads every document of a language is what follows it, because the key it names may
/// live in another file.
/// </para>
/// </remarks>
public static class LocaleReader
{
    /// <summary>Reads the strings of one document of a language.</summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="file">The name of the file the text came from, which an error mentions.</param>
    /// <returns>The strings by key.</returns>
    /// <exception cref="ArgumentException">The file is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    /// <exception cref="LocaleException">The document does not describe strings, and the error names the file and the line.</exception>
    public static IReadOnlyDictionary<string, LocaleString> Read(string text, string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        ArgumentNullException.ThrowIfNull(text);

        YamlValue document;

        try
        {
            document = YamlReader.Read(text, file);
        }
        catch (YamlException exception)
        {
            throw new LocaleException(exception.Message, file, exception.Line);
        }

        if (document is not YamlMapping mapping)
        {
            throw new LocaleException("a document of a language is a set of keys and the strings they hold, and this document holds " + Shape(document), file, document.Line);
        }

        var strings = new Dictionary<string, List<Part>>(StringComparer.Ordinal);

        foreach (YamlEntry entry in mapping.Entries)
        {
            if (entry.Value is not YamlScalar scalar || scalar.IsEmpty)
            {
                throw new LocaleException($"{file}: the key '{entry.Name}' holds {Shape(entry.Value)} where the string itself is expected", file, entry.Value.Line);
            }

            (string key, string? part) = Split(entry.Name);

            if (!strings.TryGetValue(key, out List<Part>? parts))
            {
                parts = [];
                strings[key] = parts;
            }

            if (parts.Exists(existing => string.Equals(existing.Name, part, StringComparison.Ordinal)))
            {
                throw new LocaleException($"{file}: the key '{entry.Name}' is written twice, and one key says one thing", file, entry.Line);
            }

            parts.Add(new Part(part, scalar.Text, entry.Value.Line));
        }

        var read = new Dictionary<string, LocaleString>(strings.Count, StringComparer.Ordinal);

        foreach ((string key, List<Part> parts) in strings)
        {
            read[key] = Build(file, key, parts);
        }

        return read;
    }

    /// <summary>Returns one string of a language out of what the document wrote for its key.</summary>
    private static LocaleString Build(string file, string key, List<Part> parts)
    {
        string? text = null;
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        var forms = new Dictionary<string, string>(StringComparer.Ordinal);
        var line = 0;

        foreach (Part part in parts)
        {
            line = line == 0 || part.Line < line ? part.Line : line;

            if (part.Name is null)
            {
                text = part.Text;
                continue;
            }

            if (PluralRules.TryParse(part.Name) is PluralCategory category)
            {
                if (text is not null)
                {
                    throw new LocaleException($"{file}: the key '{key}' writes its text and writes it by count, and a key says one of the two", file, part.Line);
                }

                forms[PluralRules.Name(category)] = part.Text;
                continue;
            }

            attributes[part.Name] = part.Text;
        }

        if (text is null && forms.Count == 0 && attributes.Count == 0)
        {
            throw new LocaleException($"{file}: the key '{key}' says nothing", file, line);
        }

        return new LocaleString(key, text, attributes, forms, file, line);
    }

    /// <summary>Splits what a document wrote as a name into the key and what the key says besides its text.</summary>
    private static (string Key, string? Part) Split(string name)
    {
        int dot = name.LastIndexOf('.');

        return dot <= 0 || dot == name.Length - 1 ? (name, null) : (name[..dot], name[(dot + 1)..]);
    }

    /// <summary>Names the shape of a value, which is what a message about a document says about what it found.</summary>
    private static string Shape(YamlValue value) => value switch
    {
        YamlScalar => "a word",
        YamlSequence => "a list",
        YamlMapping => "a set of keys",
        _ => "something this reader does not know",
    };

    /// <summary>What a document wrote for one key: its text, one of its attributes, or one of its forms.</summary>
    /// <param name="Name">The word after the dot of the key, or null when this is the text of the key itself.</param>
    /// <param name="Text">What the document wrote.</param>
    /// <param name="Line">The line of the document, counting from one.</param>
    private readonly record struct Part(string? Name, string Text, int Line);
}
