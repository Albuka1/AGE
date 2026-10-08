using System.Text;

namespace Age.Content.Yaml;

/// <summary>
/// Reads the subset of YAML that the content of the engine is written in: scalars, sequences, mappings and the nesting of
/// them, with comments, empty lines and the quoting of a word that would read as a flag without it.
/// </summary>
/// <remarks>
/// <para>
/// The subset is fixed on purpose. A prototype file is written by a person and read by the engine, so the reader covers
/// what such a file needs and refuses the rest with a message that names the line: no anchors, no aliases, no tags, no
/// flow style, no block scalars and no tabs in the indentation. A file that asks for more is a file that has to change
/// rather than one that is read halfway.
/// </para>
/// <para>
/// A sequence item is a scalar on the line of its dash, or a block that starts on the line below or behind the dash with
/// one level more indentation, which is what <c>- name: sword</c> means.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// YamlValue document = YamlReader.Read(text, "Prototypes/Weapons/pistol.yml");
///
/// if (document is YamlMapping mapping &amp;&amp; mapping.TryGet("parent", out YamlValue parent))
/// {
///     // parent is the prototype this one inherits from
/// }
/// </code>
/// </example>
public static class YamlReader
{
    /// <summary>Reads a document.</summary>
    /// <param name="text">The text of the document.</param>
    /// <param name="name">The name of the file the text came from, which an error mentions.</param>
    /// <returns>The value that the document holds, which is empty when the text holds nothing.</returns>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    /// <exception cref="ArgumentException">The name is null, empty or whitespace.</exception>
    /// <exception cref="YamlException">The document cannot be read, and the error names the line of the mistake.</exception>
    public static YamlValue Read(string text, string name)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Reader(text, name).ReadDocument();
    }

    /// <summary>Reads one document, which keeps the state of the reading in one place.</summary>
    private sealed class Reader
    {
        private readonly List<Line> _lines = new();
        private int _index;

        public Reader(string text, string name)
        {
            string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

            for (var number = 0; number < lines.Length; number++)
            {
                string content = StripComment(lines[number]);
                int indent = 0;

                while (indent < content.Length && content[indent] == ' ')
                {
                    indent++;
                }

                if (indent < content.Length && content[indent] == '\t')
                {
                    throw new YamlException($"{name}: a tab cannot indent a line of a document, because a tab is not a fixed number of columns", number + 1, indent + 1);
                }

                content = content[indent..].TrimEnd();

                if (content.Length > 0)
                {
                    _lines.Add(new Line(indent, content, number + 1));
                }
            }
        }

        /// <summary>Reads the value of the document, which has to be the only one it holds.</summary>
        public YamlValue ReadDocument()
        {
            if (_lines.Count == 0)
            {
                return new YamlMapping([], 1);
            }

            YamlValue value = ReadValue(_lines[0].Indent);

            if (_index < _lines.Count)
            {
                Line line = _lines[_index];
                throw new YamlException($"a document holds one value, and a second one starts here", line.Number, line.Indent + 1);
            }

            return value;
        }

        /// <summary>Reads the value that starts on the current line, which is a sequence, a mapping or a scalar.</summary>
        private YamlValue ReadValue(int indent)
        {
            Line line = _lines[_index];

            if (IsItem(line.Content))
            {
                return ReadSequence(indent);
            }

            if (YamlKey.TrySplit(line.Content, out _, out _))
            {
                return ReadMapping(indent, line);
            }

            // A document that holds one word is a value like any other, so the line it is written on is consumed here.
            _index++;
            return ReadScalar(line.Content, line.Number, indent + 1);
        }

        /// <summary>Reads the names and the values of a mapping, which all line up at one indentation.</summary>
        private YamlMapping ReadMapping(int indent, Line first)
        {
            var entries = new List<YamlEntry>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            int line = first.Number;

            while (_index < _lines.Count && _lines[_index].Indent == indent)
            {
                Line current = _lines[_index];

                if (IsItem(current.Content))
                {
                    break;
                }

                if (!YamlKey.TrySplit(current.Content, out string name, out string rest))
                {
                    throw new YamlException("a line of a mapping is a name, a colon and the value it holds", current.Number, indent + 1);
                }

                if (!names.Add(name))
                {
                    throw new YamlException($"the mapping holds the name '{name}' more than once", current.Number, indent + 1);
                }

                _index++;
                entries.Add(new YamlEntry(name, ReadValueBehind(rest, indent, current), current.Number));
            }

            EnsureAligned(indent);
            return new YamlMapping(entries, line);
        }

        /// <summary>Reads a sequence, which is the items that start with a dash at one indentation.</summary>
        private YamlSequence ReadSequence(int indent)
        {
            var items = new List<YamlValue>();
            int line = _lines[_index].Number;

            while (_index < _lines.Count && _lines[_index].Indent == indent && IsItem(_lines[_index].Content))
            {
                Line current = _lines[_index];
                string rest = current.Content.Length > 1 ? current.Content[1..].Trim() : string.Empty;

                if (rest.Length > 0 && YamlKey.TrySplit(rest, out _, out _))
                {
                    // The item is a mapping that starts behind its dash, so the rest of it lines up one level in.
                    _lines[_index] = current with { Indent = indent + 2, Content = rest };
                    items.Add(ReadMapping(indent + 2, _lines[_index]));
                    continue;
                }

                _index++;
                items.Add(ReadValueBehind(rest, indent, current));
            }

            EnsureAligned(indent);
            return new YamlSequence(items, line);
        }

        /// <summary>Reads the value behind a name or a dash: the word on the line itself, or the block that follows it.</summary>
        private YamlValue ReadValueBehind(string rest, int indent, Line line)
        {
            if (rest.Length > 0)
            {
                // The shape of the word is read first, so that a value this reader does not read says so rather than
                // looking like a block that follows another value.
                YamlScalar scalar = ReadScalar(rest, line.Number, line.Indent + 1);

                if (_index < _lines.Count && _lines[_index].Indent > indent)
                {
                    Line next = _lines[_index];
                    throw new YamlException("a value is either a word on this line or a block below it, not both", next.Number, next.Indent + 1);
                }

                return scalar;
            }

            if (_index < _lines.Count && _lines[_index].Indent > indent)
            {
                return ReadValue(_lines[_index].Indent);
            }

            return new YamlScalar(string.Empty, quoted: false, line.Number);
        }

        /// <summary>Refuses a line that is indented deeper than the block it would belong to, which is a mistake and not a nesting.</summary>
        private void EnsureAligned(int indent)
        {
            if (_index < _lines.Count && _lines[_index].Indent > indent)
            {
                Line line = _lines[_index];
                throw new YamlException("this line does not line up with the block above it", line.Number, line.Indent + 1);
            }
        }

        /// <summary>Reads a scalar, which is a word, a quoted word, or a shape that this reader does not read.</summary>
        private static YamlScalar ReadScalar(string content, int line, int column)
        {
            char first = content[0];

            if (first == '[' || first == '{')
            {
                throw new YamlException("a list or a mapping of one line, such as [a, b], is not read: the content of the engine is written in block style", line, column);
            }

            if (first == '&' || first == '*')
            {
                throw new YamlException("an anchor or an alias is not read: a document of the engine writes what it needs", line, column);
            }

            if (first == '!')
            {
                throw new YamlException("a tag is not read: the name of a field already says what its value is", line, column);
            }

            if (first == '|' || first == '>')
            {
                throw new YamlException("a block scalar is not read: a prototype holds words and numbers, not paragraphs", line, column);
            }

            if (first == '"' || first == '\'')
            {
                if (content.Length < 2 || content[^1] != first)
                {
                    throw new YamlException("a value that starts with a quote has to end with one on the same line", line, column);
                }

                string text = content[1..^1];
                return new YamlScalar(first == '"' ? Unescape(text, line, column) : text, quoted: true, line);
            }

            return new YamlScalar(content, quoted: false, line);
        }

        /// <summary>Reads the escapes of a value that a document wrote in double quotes.</summary>
        private static string Unescape(string text, int line, int column)
        {
            var result = new StringBuilder(text.Length);

            for (var index = 0; index < text.Length; index++)
            {
                if (text[index] != '\\' || index + 1 >= text.Length)
                {
                    result.Append(text[index]);
                    continue;
                }

                index++;
                result.Append(text[index] switch
                {
                    'n' => '\n',
                    't' => '\t',
                    '"' => '"',
                    '\\' => '\\',
                    _ => throw new YamlException($"the value holds the escape '\\{text[index]}', and a document is written with \\n, \\t, \\\" and \\\\", line, column),
                });
            }

            return result.ToString();
        }

        /// <summary>Removes the comment at the end of a line, which is a hash that starts a line or follows a space.</summary>
        private static string StripComment(string text)
        {
            for (var index = 0; index < text.Length; index++)
            {
                char character = text[index];

                if (character == '"' || character == '\'')
                {
                    int closing = text.IndexOf(character, index + 1);

                    if (closing < 0)
                    {
                        // The quote is closed by a mistake on another line, which the scalar of this line reports.
                        return text;
                    }

                    index = closing;
                    continue;
                }

                if (character == '#' && (index == 0 || text[index - 1] == ' '))
                {
                    return text[..index];
                }
            }

            return text;
        }

        /// <summary>Determines whether a line starts an item of a list.</summary>
        private static bool IsItem(string content) => content == "-" || content.StartsWith("- ", StringComparison.Ordinal);

        /// <summary>One line of a document that holds something, with the indentation it starts at.</summary>
        private readonly record struct Line(int Indent, string Content, int Number);
    }
}
