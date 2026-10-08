namespace Age.Core;

/// <summary>One command that a console can run.</summary>
/// <param name="Name">The word that runs the command, which is what a line of the console starts with.</param>
/// <param name="Description">A single line that the <c>help</c> command of the console prints next to the name.</param>
/// <param name="Run">Runs the command with the arguments behind its name, split on whitespace.</param>
public readonly record struct ConsoleCommand(string Name, string Description, Action<IReadOnlyList<string>> Run);
