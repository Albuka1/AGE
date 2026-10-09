namespace Age.Content.Prototypes;

/// <summary>
/// The error of a document that does not describe a prototype, which names the file and the line it was found on.
/// </summary>
/// <remarks>
/// A prototype file is content rather than code, so its mistakes are reported with the place a person has to open: the
/// file and the line carry the message, and the message says what is wrong with what is there.
/// </remarks>
public sealed class PrototypeException : Exception
{
    /// <summary>Initializes the error of a document that is not a prototype.</summary>
    /// <param name="message">What is wrong with the document, which the file and the line are added to.</param>
    /// <param name="file">The file the document came from.</param>
    /// <param name="line">The line of the file, counting from one.</param>
    public PrototypeException(string message, string file, int line)
        : base($"{message} ({file}, line {line})")
    {
        File = file;
        Line = line;
    }

    /// <summary>Gets the file the document came from.</summary>
    public string File { get; }

    /// <summary>Gets the line of the file, counting from one.</summary>
    public int Line { get; }
}
