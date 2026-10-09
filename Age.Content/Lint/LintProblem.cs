namespace Age.Content.Lint;

/// <summary>
/// One mistake of the content, with the place a person has to open to fix it.
/// </summary>
/// <param name="File">The file the document came from, relative to the game root.</param>
/// <param name="Line">The line of the file, counting from one, or zero when the mistake is about the content as a whole.</param>
/// <param name="Message">What is wrong.</param>
/// <remarks>
/// The text of the problem is written the way a compiler writes one, <c>file(line): message</c>, so a terminal and an editor
/// can both take a person to the place of the mistake.
/// </remarks>
public sealed record LintProblem(string File, int Line, string Message)
{
    /// <inheritdoc />
    public override string ToString() => Line > 0 ? $"{File}({Line}): {Message}" : $"{File}: {Message}";
}
