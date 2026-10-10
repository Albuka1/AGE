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
    private readonly Node _root = new(string.Empty, string.Empty);
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
                // The command a line names is the deepest node that its words walk to, and the rest of the line is its arguments: a
                // line whose words name no command answers nothing, and a half-typed last word still finds the command above it,
                // which is what a panel shows the description of.
                return Find(_input.ToString()) is (Node node, _) ? node.Command : null;
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

            // Only the last word is completed, and the words before it are the path that is already walked into the tree: 'cvars se'
            // resolves the group 'cvars' and completes the word 'se' to the child 'set'.
            int end = line.AsSpan().LastIndexOfAny(' ', '\t');
            string word = end < 0 ? line : line[(end + 1)..];

            if (word.Length == 0)
            {
                return false;
            }

            // The path is normalized the way a registered name is, one space between its words, so a line that was typed with a tab
            // or two spaces still walks the same nodes: the tree holds one word per level.
            string[] pathWords = (end < 0 ? string.Empty : line[..end]).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            Node node = Resolve(pathWords);

            string[] matches = [.. node.Children.Where(child => child.Word.StartsWith(word, StringComparison.OrdinalIgnoreCase)).Select(child => child.Word)];

            // A word that matches one level is completed to it; one that matches several is completed to the part they share, and a
            // word that already is that part is left alone rather than shortened to itself. The whole path is written back, which is
            // what normalizes a path that was typed with a tab or a doubled space in it.
            string completion = matches.Length switch
            {
                0 => string.Empty,
                1 => matches[0],
                _ => CommonPrefix(matches),
            };

            if (completion.Length <= word.Length)
            {
                return false;
            }

            string path = string.Join(' ', pathWords);
            _input.Clear().Append(path.Length == 0 ? completion : $"{path} {completion}");
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

    /// <summary>Returns the rows a console lists under a line, which are the levels that could still be typed at the one the line reached.</summary>
    /// <param name="prefix">The line so far, which may hold several words. An empty prefix lists the top level.</param>
    /// <remarks>
    /// A console of several levels walks a tree rather than matching whole names: the words before the last one are the path that was
    /// walked, and the words one level below that path are what the panel lists. 'cvars' lists 'set' and 'get', and 'cvars set ' lists
    /// what follows that level. A name is reported as its whole path, so a row is read as the command it completes to and a caller
    /// that writes a row into the line gets a line it can run.
    /// </remarks>
    public IEnumerable<ConsoleCommand> Matches(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        lock (_gate)
        {
            return Children(prefix);
        }
    }

    /// <summary>Returns the levels one below the line that is being typed, whose word starts with the word that is being typed at it.</summary>
    /// <param name="line">The line so far, whose last word is the one being completed.</param>
    /// <returns>The children of the node the finished words reach, in the order they were registered.</returns>
    private ConsoleCommand[] Children(string line)
    {
        int end = line.AsSpan().LastIndexOfAny(' ', '\t');

        // The last word is the part of a child that is typed, and the words before it are the path that was walked to reach the level.
        string word = end < 0 ? line : line[(end + 1)..];
        string[] pathWords = (end < 0 ? string.Empty : line[..end]).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        return [.. Resolve(pathWords).Children
            .Where(child => child.Word.StartsWith(word, StringComparison.OrdinalIgnoreCase))
            .Select(child => child.Command)];
    }

    /// <summary>Returns the node the words walk to, which is the root when they walk nowhere.</summary>
    /// <param name="words">The words of a path, in order.</param>
    private Node Resolve(string[] words)
    {
        Node node = _root;

        foreach (string word in words)
        {
            if (!node.Lookup.TryGetValue(word, out Node? child))
            {
                return node;
            }

            node = child;
        }

        return node;
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

        // A name holds words joined by single spaces, which is what makes a command of several levels: 'cvars set' is the name of the
        // leaf that a line of those two words runs, and each word before it is a group the line walks through. The tree holds one word
        // per level, so a caller that wrote a tab or two spaces still registers the name that a line walks.
        string[] words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
        {
            throw new ArgumentException("The name of a command holds at least one word.", nameof(name));
        }

        lock (_gate)
        {
            Node node = _root;

            foreach (string word in words)
            {
                node = node.Add(word);
            }

            if (node.Run is not null)
            {
                throw new ArgumentException($"A command named '{string.Join(' ', words)}' is already registered.", nameof(name));
            }

            node.Run = run;
            node.Description = description;
            node.Publish(_commands);
        }
    }

    /// <inheritdoc />
    public bool Unregister(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        lock (_gate)
        {
            string[] words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)
            {
                return false;
            }

            Node node = Resolve(words);

            if (node.Run is null)
            {
                return false;
            }

            // The node stays as a group of the levels below it: a command that is removed is no longer run, and a path that walked
            // through it still reaches what it held.
            node.Run = null;
            node.Publish(_commands);
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

        Node command;
        string[] arguments;

        lock (_gate)
        {
            // The line is walked into the tree one word at a time, and the deepest leaf it reaches runs: 'cvars set locale ru' walks
            // the groups 'cvars' and 'set' and runs the leaf 'cvars set' with 'locale' and 'ru'. A line that stops above a leaf, on a
            // node that holds a command of its own, runs that command with the words that are left.
            if (Find(line) is not (Node found, string[] rest))
            {
                WriteError($"there is no command named '{FirstWord(line)}'. Type 'help' for the list.");
                return false;
            }

            command = found;
            arguments = rest;
        }

        try
        {
            command.Run!(arguments);
        }
        catch (Exception exception)
        {
            // A command is a tool of a developer, so a failure of one is a line in the console rather than a game that
            // stops: the caller learns that the line did not run and reads why in the same place it typed it.
            WriteError($"the command '{command.Command.Name}' failed: {exception.GetType().Name}: {exception.Message}");
            return false;
        }

        return true;
    }

    /// <summary>Returns the leaf a line names and the arguments behind it, or null when no command matches the start of the line.</summary>
    /// <param name="line">The line to read.</param>
    /// <remarks>
    /// The words of the line walk the tree until a word names no child, and the deepest node that holds a command runs: the words that
    /// are left are the arguments. A node that holds no command of its own is a group of others, so a line that stops at one is left
    /// to the command above it, which is what makes 'cvars' and 'cvars set' both work.
    /// </remarks>
    private (Node Node, string[] Arguments)? Find(string line)
    {
        string[] words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        Node node = _root;
        Node? deepest = node.Run is null ? null : node;
        int reached = 0;

        for (int index = 0; index < words.Length; index++)
        {
            if (!node.Lookup.TryGetValue(words[index], out Node? child))
            {
                break;
            }

            node = child;

            if (node.Run is not null)
            {
                // The depth is remembered with the command, so the words left over are the arguments of the deepest command the line
                // reached rather than of the level the walk ended at, which may be a group of others.
                deepest = node;
                reached = index + 1;
            }
        }

        return deepest is null ? null : (deepest, [.. words[reached..]]);
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

    /// <summary>Returns the longest start that every word of a set shares, which is what a word that matches several is completed to.</summary>
    /// <param name="names">The words that a typed word matched, which holds at least one.</param>
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

    /// <summary>One level of the tree of commands: a word, what it does when a line ends there, and the levels below it.</summary>
    /// <param name="word">The word that names this level, which is empty for the root.</param>
    /// <param name="description">What a line that ends here does, or empty for a group that only holds others.</param>
    /// <remarks>
    /// A level that holds a command runs it when a line ends there, and a level that holds none is a group of the levels below it: a
    /// level may be both, which is what lets 'cvars' list the settings and 'cvars set locale ru' walk through it to a leaf.
    /// </remarks>
    private sealed class Node(string word, string description)
    {
        private readonly List<Node> _children = [];

        /// <summary>Gets the word that names this level.</summary>
        public string Word { get; } = word;

        /// <summary>Gets or sets what a line that ends here does, or empty for a group.</summary>
        public string Description { get; set; } = description;

        /// <summary>Gets or sets the command of this level, or null when the level only holds the levels below it.</summary>
        public Action<IReadOnlyList<string>>? Run { get; set; }

        /// <summary>Gets the levels below this one, in the order they were added.</summary>
        public IEnumerable<Node> Children => _children;

        /// <summary>Gets the children of this level by their word, which is what a line walks down the tree by.</summary>
        public Dictionary<string, Node> Lookup { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets the command of this level as the row a console lists, which is its whole path from the root.</summary>
        public ConsoleCommand Command => new(Path(), Description, Run ?? (_ => { }));

        /// <summary>Returns the level of a word below this one, adding it when it is not there.</summary>
        /// <param name="word">The word of the level.</param>
        /// <returns>The level, which is the one that was already there or the one that was added.</returns>
        public Node Add(string word)
        {
            if (Lookup.TryGetValue(word, out Node? child))
            {
                return child;
            }

            child = new Node(word, string.Empty) { _parent = this };
            _children.Add(child);
            Lookup[word] = child;
            return child;
        }

        /// <summary>Writes the command of this level into the flat list a console reads, adding it or replacing what was there.</summary>
        /// <param name="commands">The list of rows that <see cref="Commands"/> answers, in the order the commands were registered.</param>
        /// <remarks>
        /// A level that holds no command is left out, so the list reads as the commands of the console rather than as every group of
        /// the tree. A level that gained a command keeps the place it was first seen at, which keeps the list stable as a game adds
        /// the levels of a command one at a time.
        /// </remarks>
        public void Publish(List<ConsoleCommand> commands)
        {
            string path = Path();
            int at = commands.FindIndex(command => string.Equals(command.Name, path, StringComparison.OrdinalIgnoreCase));

            if (Run is null)
            {
                if (at >= 0)
                {
                    commands.RemoveAt(at);
                }

                return;
            }

            var row = new ConsoleCommand(path, Description, Run);

            if (at >= 0)
            {
                commands[at] = row;
                return;
            }

            commands.Add(row);
        }

        /// <summary>Returns the words of the path from the root to this level, joined by single spaces.</summary>
        private string Path()
        {
            if (Word.Length == 0)
            {
                return string.Empty;
            }

            return _parent is null || _parent.Word.Length == 0 ? Word : $"{_parent.Path()} {Word}";
        }

        private Node? _parent;
    }
}
