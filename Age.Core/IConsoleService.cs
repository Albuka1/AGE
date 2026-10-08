namespace Age.Core;

/// <summary>
/// A console that a developer uses while a game runs: the commands it knows, the lines that were entered, and the lines
/// that were written.
/// </summary>
/// <remarks>
/// <para>
/// The service holds the state of a console and none of its presentation: the engine draws it and a game registers the
/// commands that belong to it, which keeps a command testable without a window. A game opens it with a key of its own,
/// and pauses the simulation while it is open, which is what <see cref="IsOpen"/> is for.
/// </para>
/// <para>
/// The console is safe to use from more than one thread, so a loader that logs while the frame thread draws the lines
/// does not disturb it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// console.Register("spawn", "Puts n sprites on screen. Defaults to one.", arguments =>
/// {
///     int count = arguments.Count > 0 &amp;&amp; int.TryParse(arguments[0], out int parsed) ? parsed : 1;
///
///     for (int index = 0; index &lt; count; index++)
///     {
///         // create a sprite
///     }
/// });
/// </code>
/// </example>
public interface IConsoleService
{
    /// <summary>Gets a value indicating whether the console is open.</summary>
    bool IsOpen { get; }

    /// <summary>Gets the line that is being typed, which is empty when nothing was typed yet.</summary>
    string Input { get; }

    /// <summary>Gets the lines that were written, oldest first, which is a snapshot that later writes do not change.</summary>
    IReadOnlyList<string> Output { get; }

    /// <summary>Gets the lines that were entered, oldest first, which <see cref="RecallPrevious"/> and <see cref="RecallNext"/> walk.</summary>
    IReadOnlyList<string> History { get; }

    /// <summary>Gets the commands that are registered, in the order they were registered.</summary>
    IEnumerable<ConsoleCommand> Commands { get; }

    /// <summary>Gets or sets the number of lines that <see cref="Output"/> keeps. The oldest lines fall off when it is reached.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The capacity is zero or negative.</exception>
    int OutputCapacity { get; set; }

    /// <summary>Opens the console.</summary>
    void Open();

    /// <summary>Closes the console, which keeps the lines it holds.</summary>
    void Close();

    /// <summary>Opens the console when it is closed and closes it when it is open.</summary>
    void Toggle();

    /// <summary>Appends a line to the output.</summary>
    /// <param name="line">The line to append.</param>
    /// <exception cref="ArgumentNullException">The line is null.</exception>
    void Write(string line);

    /// <summary>Appends a line that reports a failure, marked so that it stands out where the console is drawn.</summary>
    /// <param name="line">The line to append.</param>
    /// <exception cref="ArgumentNullException">The line is null.</exception>
    void WriteError(string line);

    /// <summary>Appends every line of a sequence, in order.</summary>
    /// <param name="lines">The lines to append.</param>
    /// <exception cref="ArgumentNullException">The lines are null.</exception>
    void WriteLines(IEnumerable<string> lines);

    /// <summary>Appends a character to the line that is being typed.</summary>
    /// <param name="character">The character to append. A control character, including the line feed that ends a line, is ignored: a line is run by <see cref="Submit"/>.</param>
    void Type(char character);

    /// <summary>Removes the last character of the line that is being typed, if there is one.</summary>
    void Backspace();

    /// <summary>Runs the line that is being typed, remembers it in the history and clears it.</summary>
    /// <returns><see langword="true"/> when the line named a command that ran, <see langword="false"/> for an empty line, an unknown command or one that failed.</returns>
    bool Submit();

    /// <summary>Replaces the line that is being typed with the line before the one the history points at.</summary>
    void RecallPrevious();

    /// <summary>Replaces the line that is being typed with the line after the one the history points at, which is an empty line at the end of the history.</summary>
    void RecallNext();

    /// <summary>Removes every line of the output.</summary>
    void Clear();

    /// <summary>Registers a command, which is how a game adds one of its own.</summary>
    /// <param name="name">The word that runs the command. It has to be unique and must not hold whitespace.</param>
    /// <param name="description">A single line that describes what the command does.</param>
    /// <param name="run">Runs the command with the arguments behind its name.</param>
    /// <exception cref="ArgumentException">The name is null, empty, holds whitespace or is already registered.</exception>
    /// <exception cref="ArgumentNullException">The description or the code to run is null.</exception>
    void Register(string name, string description, Action<IReadOnlyList<string>> run);

    /// <summary>Removes a command that <see cref="Register"/> added.</summary>
    /// <param name="name">The name of the command, matched without regard to case.</param>
    /// <returns><see langword="true"/> when the command was registered and has been removed.</returns>
    bool Unregister(string name);

    /// <summary>Runs a line as a command, without touching the history or the line that is being typed.</summary>
    /// <param name="line">The line to run, such as <c>spawn 3</c>.</param>
    /// <returns><see langword="true"/> when the line named a command that ran.</returns>
    /// <exception cref="ArgumentNullException">The line is null.</exception>
    bool Execute(string line);
}
