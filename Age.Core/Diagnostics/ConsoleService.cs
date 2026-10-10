using System.Text;

namespace Age.Core;

/// <summary>
/// The default <see cref="IConsoleService"/>: the commands it knows, the lines that were entered, and the lines that were
/// written.
/// </summary>
/// <remarks>
/// The service holds the state of a console and none of its presentation, so the engine draws it with whatever it draws
/// text with and a game drives it from its own input. Two commands are always there: <c>help</c>, which lists what is
/// registered, and <c>clear</c>, which empties the output. Names are matched without regard to case, so <c>Help</c> runs
/// the same command as <c>help</c>.
/// </remarks>
public sealed class ConsoleService : IConsoleService
{
    private const int DefaultOutputCapacity = 200;

    private readonly object _gate = new();
    private readonly List<ConsoleLine> _output = new();
    private readonly List<string> _history = new();
    private readonly List<ConsoleCommand> _commands = new();
    private readonly Dictionary<string, ConsoleCommand> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly StringBuilder _input = new();
    private int _recall = -1;
    private int _outputCapacity = DefaultOutputCapacity;

    /// <summary>Initializes the service with the two commands that are always there, <c>help</c> and <c>clear</c>.</summary>
    public ConsoleService()
    {
        Register("help", "Lists the commands and what they do.", _ => WriteLines(Help()));
        Register("clear", "Removes every line of the output.", _ => Clear());
    }

    /// <inheritdoc />
    public bool IsOpen { get; private set; }

    /// <inheritdoc />
    public string Input
    {
        get
        {
            lock (_gate)
            {
                return _input.ToString();
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Output
    {
        get
        {
            lock (_gate)
            {
                return [.. _output.Select(line => line.Text)];
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ConsoleLine> Lines
    {
        get
        {
            lock (_gate)
            {
                return [.. _output];
            }
        }
    }

    /// <inheritdoc />
    public ConsoleCommand? Hint
    {
        get
        {
            lock (_gate)
            {
                // The command a line names is the longest path of words that a registered name matches, and the rest of the line
                // is its arguments: a line whose words name no command answers nothing, and a half-typed last word still finds
                // the command above it, which is what a panel shows the description of.
                return Find(_input.ToString()) is (ConsoleCommand command, _) ? command : null;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> History
    {
        get
        {
            lock (_gate)
            {
                return [.. _history];
            }
        }
    }

    /// <inheritdoc />
    public IEnumerable<ConsoleCommand> Commands
    {
        get
        {
            lock (_gate)
            {
                return [.. _commands];
            }
        }
    }

    /// <inheritdoc />
    public int OutputCapacity
    {
        get => _outputCapacity;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

            lock (_gate)
            {
                _outputCapacity = value;

                while (_output.Count > _outputCapacity)
                {
                    _output.RemoveAt(0);
                }
            }
        }
    }

    /// <inheritdoc />
    public void Open() => IsOpen = true;

    /// <inheritdoc />
    public void Close() => IsOpen = false;

    /// <inheritdoc />
    public void Toggle() => IsOpen = !IsOpen;

    /// <inheritdoc />
    public void Write(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        Append(line, ConsoleLevel.Normal);
    }

    /// <inheritdoc />
    public void WriteError(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        Append(line, ConsoleLevel.Error);
    }

    /// <inheritdoc />
    public void WriteWarning(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        Append(line, ConsoleLevel.Warning);
    }

    /// <inheritdoc />
    public void Type(char character)
    {
        // A control character, which is what a key like Enter or Tab produces, never belongs to a line that is typed: the
        // console runs a line through Submit, and the engine maps the rest of the keys.
        if (char.IsControl(character))
        {
            return;
        }

        lock (_gate)
        {
            _input.Append(character);
            _recall = -1;
        }
    }

    /// <inheritdoc />
    public void Backspace()
    {
        lock (_gate)
        {
            if (_input.Length > 0)
            {
                _input.Length--;
                _recall = -1;
            }
        }
    }

    /// <inheritdoc />
    public bool Submit()
    {
        string line;

        lock (_gate)
        {
            line = _input.ToString();
            _input.Clear();
            _recall = -1;
        }

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        lock (_gate)
        {
            _history.Add(line);
        }

        return Execute(line);
    }

    /// <inheritdoc />
    public void RecallPrevious()
    {
        lock (_gate)
        {
            if (_history.Count == 0)
            {
                return;
            }

            _recall = _recall < 0 ? _history.Count - 1 : Math.Max(0, _recall - 1);
            _input.Clear().Append(_history[_recall]);
        }
    }

    /// <inheritdoc />
    public void RecallNext()
    {
        lock (_gate)
        {
            if (_recall < 0)
            {
                return;
            }

            _recall++;

            if (_recall >= _history.Count)
            {
                // Walking past the last line leaves an empty line, which is what the developer had before the recall.
                _recall = -1;
                _input.Clear();
                return;
            }

            _input.Clear().Append(_history[_recall]);
        }
    }

    /// <inheritdoc />
    public bool Complete()
    {
        lock (_gate)
        {
            string line = _input.ToString();

            // Only the last word is completed, and the words before it are the path of the command: 'cvars se' completes the word
            // 'se' to the command 'cvars set', which is what a console of several levels does.
            int end = line.AsSpan().LastIndexOfAny(' ', '\t');
            string path = end < 0 ? string.Empty : line[..(end + 1)];
            string word = end < 0 ? line : line[(end + 1)..];

            if (word.Length == 0)
            {
                return false;
            }

            string[] matches = [.. _byName.Keys.Where(name => name.StartsWith(path + word, StringComparison.OrdinalIgnoreCase))];

            // A word that matches one command is completed to it; one that matches several is completed to the part they share,
            // and a word that already is that part is left alone rather than shortened to itself. Only the word is replaced, so
            // the path that is already typed stays.
            string completion = matches.Length switch
            {
                0 => string.Empty,
                1 => matches[0],
                _ => CommonPrefix(matches),
            };

            if (completion.Length <= path.Length + word.Length)
            {
                return false;
            }

            _input.Clear().Append(completion);
            _recall = -1;
            return true;
        }
    }

    /// <inheritdoc />
    public void SetInput(string line)
    {
        lock (_gate)
        {
            _input.Clear().Append(line ?? string.Empty);
            _recall = -1;
        }
    }

    /// <summary>Returns the commands whose name starts with a prefix, in the order they were registered, which is what a console lists as suggestions.</summary>
    /// <param name="prefix">The start of a name to match, which may hold several words. An empty prefix matches every command.</param>
    /// <remarks>A name that starts with more words than the prefix counts as a match, so 'cvars' lists 'cvars set' as well: what a panel shows is what the line could still become.</remarks>
    public IEnumerable<ConsoleCommand> Matches(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        lock (_gate)
        {
            return [.. _commands.Where(command => command.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))];
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_gate)
        {
            _output.Clear();
        }
    }

    /// <inheritdoc />
    public void Register(string name, string description, Action<IReadOnlyList<string>> run)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(run);

        // A name holds words joined by single spaces, which is what makes a command of several levels: 'cvars set' is the name
        // of the command that a line of those two words runs. The name is stored with one space between its words, so a caller
        // that wrote a tab or two spaces still registers the name that a line matches.
        string[] words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
        {
            throw new ArgumentException("The name of a command holds at least one word.", nameof(name));
        }

        name = string.Join(' ', words);

        lock (_gate)
        {
            if (_byName.ContainsKey(name))
            {
                throw new ArgumentException($"A command named '{name}' is already registered.", nameof(name));
            }

            var command = new ConsoleCommand(name, description, run);
            _commands.Add(command);
            _byName[name] = command;
        }
    }

    /// <inheritdoc />
    public bool Unregister(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        lock (_gate)
        {
            if (!_byName.Remove(name, out ConsoleCommand command))
            {
                return false;
            }

            _commands.Remove(command);
            return true;
        }
    }

    /// <inheritdoc />
    public bool Execute(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return false;
        }

        ConsoleCommand command;
        string[] arguments;

        lock (_gate)
        {
            // The name of a command may hold more than one word, so the line is read from its start for the longest name that
            // matches: 'cvars set locale ru' runs the command 'cvars set' with 'locale' and 'ru', and a command of one word is
            // the same case with an empty argument list.
            if (Find(line) is not (ConsoleCommand found, string[] rest))
            {
                WriteError($"there is no command named '{FirstWord(line)}'. Type 'help' for the list.");
                return false;
            }

            command = found;
            arguments = rest;
        }

        try
        {
            command.Run(arguments);
        }
        catch (Exception exception)
        {
            // A command is a tool of a developer, so a failure of one is a line in the console rather than a game that
            // stops: the caller learns that the line did not run and reads why in the same place it typed it.
            WriteError($"the command '{command.Name}' failed: {exception.GetType().Name}: {exception.Message}");
            return false;
        }

        return true;
    }

    /// <summary>Returns the command a line names and the arguments behind it, or null when no name matches the start of the line.</summary>
    /// <param name="line">The line to read.</param>
    /// <remarks>
    /// A name holds one or more words, so the words of the line are tried as a name from the longest run to the shortest: the
    /// command of the most words wins, which is what lets 'cvars' and 'cvars set' both exist and the longer one run for the line
    /// that names it. The words that are left are the arguments.
    /// </remarks>
    private (ConsoleCommand Command, string[] Arguments)? Find(string line)
    {
        string[] words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        for (int count = words.Length; count >= 1; count--)
        {
            if (_byName.TryGetValue(string.Join(' ', words[..count]), out ConsoleCommand command))
            {
                return (command, [.. words[count..]]);
            }
        }

        return null;
    }

    /// <summary>Returns what the <c>help</c> command prints: one line per command, in the order they were registered.</summary>
    public IEnumerable<string> Help()
    {
        lock (_gate)
        {
            return _commands.Select(command => $"{command.Name} - {command.Description}").ToArray();
        }
    }

    /// <inheritdoc />
    public void WriteLines(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        foreach (string line in lines)
        {
            Write(line);
        }
    }

    private void Append(string line, ConsoleLevel level)
    {
        lock (_gate)
        {
            // The text of an error keeps the prefix that a console of a terminal shows, because Output answers text; the level
            // travels beside it, which is what a renderer colours the line by.
            string text = level == ConsoleLevel.Error ? $"error: {line}" : line;
            _output.Add(new ConsoleLine(text, level));

            while (_output.Count > _outputCapacity)
            {
                _output.RemoveAt(0);
            }
        }
    }

    /// <summary>Returns the first word of a line, which is the part of it before the first whitespace.</summary>
    private static string FirstWord(string line)
    {
        int end = line.AsSpan().IndexOfAny(' ', '\t');

        return end < 0 ? line : line[..end];
    }

    /// <summary>Returns the longest start that every name of a set shares, which is what a word that matches several is completed to.</summary>
    private static string CommonPrefix(string[] names)
    {
        string prefix = names[0];

        foreach (string name in names)
        {
            int length = 0;

            while (length < prefix.Length && length < name.Length && char.ToUpperInvariant(prefix[length]) == char.ToUpperInvariant(name[length]))
            {
                length++;
            }

            prefix = prefix[..length];

            if (prefix.Length == 0)
            {
                break;
            }
        }

        return prefix;
    }
}
