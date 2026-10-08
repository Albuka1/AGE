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
    private readonly List<string> _output = new();
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
                return [.. _output];
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
        Append(line, isError: false);
    }

    /// <inheritdoc />
    public void WriteError(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        Append(line, isError: true);
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

        if (name.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException($"The name of a command cannot hold whitespace, but '{name}' does.", nameof(name));
        }

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

        lock (_gate)
        {
            if (!_byName.TryGetValue(parts[0], out command))
            {
                WriteError($"there is no command named '{parts[0]}'. Type 'help' for the list.");
                return false;
            }
        }

        try
        {
            command.Run([.. parts[1..]]);
        }
        catch (Exception exception)
        {
            // A command is a tool of a developer, so a failure of one is a line in the console rather than a game that
            // stops: the caller learns that the line did not run and reads why in the same place it typed it.
            WriteError($"the command '{parts[0]}' failed: {exception.GetType().Name}: {exception.Message}");
            return false;
        }

        return true;
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

    private void Append(string line, bool isError)
    {
        lock (_gate)
        {
            _output.Add(isError ? $"error: {line}" : line);

            while (_output.Count > _outputCapacity)
            {
                _output.RemoveAt(0);
            }
        }
    }
}
