namespace Age.Content.Yaml;

/// <summary>
/// One value of a YAML document: a scalar, a sequence or a mapping.
/// </summary>
/// <remarks>
/// The content of the engine is written by people, so a value remembers the line it was read from: a message that refuses
/// a document names the file, the line and what was wrong with it, which is the difference between a typo that costs a
/// minute and one that costs an afternoon.
/// </remarks>
public abstract class YamlValue
{
    /// <summary>Initializes a value that was read from a line of a document.</summary>
    /// <param name="line">The line of the document, counting from one.</param>
    protected YamlValue(int line) => Line = line;

    /// <summary>Gets the line of the document that the value was read from, counting from one.</summary>
    public int Line { get; }
}

/// <summary>A single value, which is text that the reader of a prototype turns into whatever field it belongs to.</summary>
public sealed class YamlScalar : YamlValue
{
    /// <summary>Initializes a scalar.</summary>
    /// <param name="text">The text of the value, with the quotes of the document removed.</param>
    /// <param name="quoted">A value indicating whether the document wrote the value in quotes.</param>
    /// <param name="line">The line of the document, counting from one.</param>
    public YamlScalar(string text, bool quoted, int line)
        : base(line)
    {
        Text = text;
        Quoted = quoted;
    }

    /// <summary>Gets the text of the value.</summary>
    public string Text { get; }

    /// <summary>Gets a value indicating whether the document wrote the value in quotes, which is what keeps <c>"true"</c> a word rather than a flag.</summary>
    public bool Quoted { get; }

    /// <summary>Gets a value indicating whether the value says nothing, which is what a key with nothing behind it holds.</summary>
    public bool IsEmpty => Text.Length == 0;

    /// <inheritdoc />
    public override string ToString() => Text;
}

/// <summary>A list of values.</summary>
public sealed class YamlSequence : YamlValue
{
    /// <summary>Initializes a sequence.</summary>
    /// <param name="items">The items of the list, in the order the document lists them.</param>
    /// <param name="line">The line of the document, counting from one.</param>
    public YamlSequence(IReadOnlyList<YamlValue> items, int line)
        : base(line) => Items = items;

    /// <summary>Gets the items of the list, in the order the document lists them.</summary>
    public IReadOnlyList<YamlValue> Items { get; }
}

/// <summary>A set of names and the values they hold, in the order the document writes them.</summary>
public sealed class YamlMapping : YamlValue
{
    /// <summary>Initializes a mapping.</summary>
    /// <param name="entries">The names and the values, in the order the document writes them.</param>
    /// <param name="line">The line of the document, counting from one.</param>
    public YamlMapping(IReadOnlyList<YamlEntry> entries, int line)
        : base(line) => Entries = entries;

    /// <summary>Gets the names and the values, in the order the document writes them.</summary>
    public IReadOnlyList<YamlEntry> Entries { get; }

    /// <summary>Returns the value that a name holds.</summary>
    /// <param name="name">The name to look up, which is matched exactly.</param>
    /// <param name="value">Receives the value when the mapping holds the name.</param>
    /// <returns><see langword="true"/> when the mapping holds the name.</returns>
    public bool TryGet(string name, out YamlValue value)
    {
        ArgumentNullException.ThrowIfNull(name);

        foreach (YamlEntry entry in Entries)
        {
            if (string.Equals(entry.Name, name, StringComparison.Ordinal))
            {
                value = entry.Value;
                return true;
            }
        }

        value = null!;
        return false;
    }
}

/// <summary>One name of a mapping and the value behind it.</summary>
/// <param name="Name">The name, which is what a prototype field or a component name looks like.</param>
/// <param name="Value">The value the name holds.</param>
/// <param name="Line">The line of the document the name was written on, counting from one.</param>
public readonly record struct YamlEntry(string Name, YamlValue Value, int Line);
