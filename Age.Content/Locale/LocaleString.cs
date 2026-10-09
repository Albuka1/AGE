namespace Age.Content.Locale;

/// <summary>
/// One string of one language: the text under a key, the attributes it carries, and the forms it writes for a count.
/// </summary>
/// <param name="Key">The key the language declares, such as <c>ent-Goblin</c>.</param>
/// <param name="Text">The text of the key, or null when the key holds attributes or forms rather than one text.</param>
/// <param name="Attributes">What the key says besides its text, by the name a document writes after the dot, such as <c>desc</c>.</param>
/// <param name="Forms">What the key writes for a count, by the name of the form, such as <c>one</c> or <c>few</c>.</param>
/// <param name="File">The document the string was read from.</param>
/// <param name="Line">The line of the document the key was written on, counting from one.</param>
/// <remarks>
/// A text that is written as <c>{ key }</c> is a reference to another key rather than a text of its own: the service that reads
/// the languages resolves it, so what this holds is what the document wrote.
/// </remarks>
public sealed record LocaleString(
    string Key,
    string? Text,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyDictionary<string, string> Forms,
    string File,
    int Line)
{
    /// <summary>Gets a value indicating whether the key writes its text by count rather than once.</summary>
    public bool HasForms => Forms.Count > 0;

    /// <inheritdoc />
    public override string ToString() => $"the string '{Key}'";
}
