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

    /// <summary>Gets or sets the index in <see cref="Input"/> where the next character is inserted, which is where a caret is drawn.</summary>
    /// <remarks>
    /// The caret is kept inside the line, so a game that draws it does not test the bounds: a value below zero or beyond the length
    /// of the line is clamped. Moving it clears the selection, which is what a key that only moves a caret does.
    /// </remarks>
    int Caret { get; set; }

    /// <summary>Gets or sets the index in <see cref="Input"/> where the selection is anchored, which is where it began.</summary>
    /// <remarks>
    /// The selection runs between <see cref="SelectionAnchor"/> and <see cref="Caret"/> in whichever order they are in, so a person
    /// that selected to the left and one that selected to the right hold the same text. Setting it does not move the caret: the two
    /// together say which text is selected.
    /// </remarks>
    int SelectionAnchor { get; set; }

    /// <summary>Gets the index in <see cref="Input"/> where the selected text begins, which is the lesser of the anchor and the caret.</summary>
    int SelectionStart { get; }

    /// <summary>Gets the number of characters that are selected, which is zero when nothing is selected.</summary>
    int SelectionLength { get; }

    /// <summary>Gets the text that is selected, which is empty when nothing is selected.</summary>
    string Selection { get; }

    /// <summary>Moves the caret by a number of characters, keeping it inside the line and clearing the selection.</summary>
    /// <param name="by">The number of characters to move by, where a negative number moves toward the start of the line.</param>
    void MoveCaret(int by);

    /// <summary>Selects the whole line that is being typed, which is what a select all key does.</summary>
    void SelectAll();

    /// <summary>Removes the selected text from the line, leaving the caret where the selection began.</summary>
    /// <returns><see langword="true"/> when something was selected and has been removed.</returns>
    bool RemoveSelection();

    /// <summary>Replaces the selected text with a text, which is what pasting over a selection does.</summary>
    /// <param name="text">The text to write in place of the selection. A null text writes nothing and only removes the selection.</param>
    /// <remarks>A control character of the text is dropped, the way <see cref="Type"/> drops one, so a line that is pasted from a document does not carry a line break into the console.</remarks>
    void ReplaceSelection(string? text);

    /// <summary>Gets the lines that were written, oldest first, which is a snapshot that later writes do not change.</summary>
    IReadOnlyList<string> Output { get; }

    /// <summary>Gets the lines that were written with the severity each of them was written at, oldest first.</summary>
    /// <remarks>
    /// This is what a renderer colours a line by: <see cref="Output"/> drops the severity, because a prefix of text is enough for
    /// a console of a terminal and not for one that draws. The two hold the same lines in the same order, so a caller that draws
    /// the output reads this one.
    /// </remarks>
    IReadOnlyList<ConsoleLine> Lines { get; }

    /// <summary>Gets the lines that were entered, oldest first, which <see cref="RecallPrevious"/> and <see cref="RecallNext"/> walk.</summary>
    IReadOnlyList<string> History { get; }

    /// <summary>Gets the commands that are registered, in the order they were registered.</summary>
    IEnumerable<ConsoleCommand> Commands { get; }

    /// <summary>Returns the levels one below the line that is being typed, which is what a console lists under it.</summary>
    /// <param name="prefix">The line so far, which may hold several words. An empty prefix lists the top level.</param>
    /// <returns>The commands, in the order they were registered, each named by its whole path.</returns>
    /// <exception cref="ArgumentNullException">The prefix is null.</exception>
    /// <remarks>
    /// A command of several levels is a tree: the words before the last one are the path that was walked, and the commands one level
    /// below it whose word starts with the last word are what a panel lists. So <c>cvars</c> lists <c>cvars set</c> and
    /// <c>cvars set </c> lists what follows that level.
    /// </remarks>
    IEnumerable<ConsoleCommand> Matches(string prefix);

    /// <summary>Gets the command that the line being typed names, or null when the line names none.</summary>
    /// <remarks>
    /// The first word of the line is matched without regard to case, so a line that is half typed still finds its command: this
    /// is what a console shows the value and the description of. A line that names only arguments, or a word that no command
    /// matches, answers null.
    /// </remarks>
    ConsoleCommand? Hint { get; }

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

    /// <summary>Appends a line that reports something a developer should look at.</summary>
    /// <param name="line">The line to append.</param>
    /// <exception cref="ArgumentNullException">The line is null.</exception>
    void WriteWarning(string line);

    /// <summary>Appends every line of a sequence, in order.</summary>
    /// <param name="lines">The lines to append.</param>
    /// <exception cref="ArgumentNullException">The lines are null.</exception>
    void WriteLines(IEnumerable<string> lines);

    /// <summary>Inserts a character at the caret of the line that is being typed, replacing the selection when there is one.</summary>
    /// <param name="character">The character to insert. A control character, including the line feed that ends a line, is ignored: a line is run by <see cref="Submit"/>.</param>
    /// <remarks>The caret moves past the character that was inserted, so typing a word writes it where the caret stands and leaves the caret at its end.</remarks>
    void Type(char character);

    /// <summary>Removes the selection, or the character before the caret when nothing is selected.</summary>
    void Backspace();

    /// <summary>Removes the selection, or the character after the caret when nothing is selected.</summary>
    void Delete();

    /// <summary>Runs the line that is being typed, remembers it in the history and clears it.</summary>
    /// <returns><see langword="true"/> when the line named a command that ran, <see langword="false"/> for an empty line, an unknown command or one that failed.</returns>
    bool Submit();

    /// <summary>Replaces the line that is being typed with the line before the one the history points at.</summary>
    void RecallPrevious();

    /// <summary>Replaces the line that is being typed with the line after the one the history points at, which is an empty line at the end of the history.</summary>
    void RecallNext();

    /// <summary>Completes the word that is being typed to a level of the command it walks to, which is what Tab does in a console of its own.</summary>
    /// <returns><see langword="true"/> when the word was completed, <see langword="false"/> when nothing matches it or it is already a whole word.</returns>
    /// <remarks>
    /// A word that matches exactly one level below the path is completed to it. A word that matches several is completed to the part
    /// they share, so pressing Tab twice narrows the word down rather than cycling through the levels. The words before the last one
    /// are the path that is already walked, so each word of a command of several levels is completed in turn.
    /// </remarks>
    bool Complete();

    /// <summary>Replaces the line that is being typed with the line this names, which is what a console takes a suggestion with.</summary>
    /// <param name="line">The line to hold. A line that is null or empty leaves the line that is being typed empty.</param>
    void SetInput(string line);

    /// <summary>Removes every line of the output.</summary>
    void Clear();

    /// <summary>Registers a command at a path of words, which is how a game adds one of its own.</summary>
    /// <param name="name">The words that run the command, joined by single spaces. The whole path has to be unique.</param>
    /// <param name="description">A single line that describes what the command does.</param>
    /// <param name="run">Runs the command with the arguments behind its path.</param>
    /// <remarks>
    /// A name holds one or more words, so a command has levels: <c>cvars</c> lists the settings, <c>cvars set</c> writes one, and
    /// <c>cvars set locale ru</c> runs the command named <c>cvars set</c> with the arguments <c>locale</c> and <c>ru</c>. Each word
    /// before the last is a group that a line walks through, and a group may hold a command of its own, which is what makes
    /// <c>cvars</c> and <c>cvars set</c> both work. The words of a path are the levels a console completes one at a time.
    /// </remarks>
    /// <exception cref="ArgumentException">The name is null, empty or already registered.</exception>
    /// <exception cref="ArgumentNullException">The description or the code to run is null.</exception>
    void Register(string name, string description, Action<IReadOnlyList<string>> run);

    /// <summary>Removes a command that <see cref="Register"/> added.</summary>
    /// <param name="name">The name of the command, matched without regard to case.</param>
    /// <returns><see langword="true"/> when the command was registered and has been removed.</returns>
    bool Unregister(string name);

    /// <summary>Runs a line as a command, without touching the history or the line that is being typed.</summary>
    /// <param name="line">The line to run, such as <c>spawn 3</c>.</param>
    /// <returns><see langword="true"/> when the line named a command that ran, <see langword="false"/> for an empty line, an unknown command or one that failed.</returns>
    /// <exception cref="ArgumentNullException">The line is null.</exception>
    /// <remarks>A command that throws does not reach the caller: the console reports what happened in the line it wrote and answers <see langword="false"/>.</remarks>
    bool Execute(string line);
}
