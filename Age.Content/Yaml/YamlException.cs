namespace Age.Content.Yaml;

/// <summary>
/// The error of a document that cannot be read, which names the line and the column it was found on.
/// </summary>
/// <remarks>
/// The message of the error says what was wrong, and the position says where to look: a document that a person wrote is
/// read with the position of the mistake rather than with a stack trace.
/// </remarks>
public sealed class YamlException : Exception
{
    /// <summary>Initializes the error of a broken document.</summary>
    /// <param name="message">What was wrong with the document.</param>
    /// <param name="line">The line of the document, counting from one.</param>
    /// <param name="column">The column of the line, counting from one.</param>
    public YamlException(string message, int line, int column)
        : base($"{message} (line {line}, column {column})")
    {
        Line = line;
        Column = column;
    }

    /// <summary>Gets the line of the document, counting from one.</summary>
    public int Line { get; }

    /// <summary>Gets the column of the line, counting from one.</summary>
    public int Column { get; }
}
