namespace Age.Content.Locale;

/// <summary>
/// Refuses a document of a language, and names the file and the line of the mistake.
/// </summary>
/// <remarks>
/// A document of a language is written by a person, so the one thing a message has to carry is where to look: the file it
/// came from and the line of the mistake, which is what the rest of the content of the engine does as well.
/// </remarks>
public sealed class LocaleException : Exception
{
    /// <summary>Initializes the exception with what is wrong and where it was read.</summary>
    /// <param name="message">What is wrong with the document.</param>
    /// <param name="file">The document the mistake is in.</param>
    /// <param name="line">The line of the document, counting from one, or zero when the mistake is the file itself.</param>
    public LocaleException(string message, string file, int line)
        : base(message)
    {
        File = file;
        Line = line;
    }

    /// <summary>Gets the document the mistake is in.</summary>
    public string File { get; }

    /// <summary>Gets the line of the document, counting from one, or zero when the mistake is the file itself.</summary>
    public int Line { get; }
}
