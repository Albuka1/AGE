namespace Age.Core;

/// <summary>The severity of a line of the console, which is what a renderer draws it in a colour by.</summary>
public enum ConsoleLevel
{
    /// <summary>An ordinary line, such as one that a command wrote.</summary>
    Normal,

    /// <summary>A line that reports something a developer should look at, such as a value that was refused.</summary>
    Warning,

    /// <summary>A line that reports a failure.</summary>
    Error,
}

/// <summary>One line of the output of a console, with the severity it was written at.</summary>
/// <param name="Text">The characters of the line.</param>
/// <param name="Level">The severity of the line.</param>
public readonly record struct ConsoleLine(string Text, ConsoleLevel Level);
