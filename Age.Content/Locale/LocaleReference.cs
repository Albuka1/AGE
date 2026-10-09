namespace Age.Content.Locale;

/// <summary>
/// Reads the reference that one key makes to another, which is how a string is inherited rather than written twice.
/// </summary>
/// <remarks>
/// <para>
/// A reference is written with the braces and a space inside them, <c>{ ent-Base }</c>, and it means «what that key says»: the
/// text of <c>ent-Base</c> when it stands under a key, the attribute <c>desc</c> of <c>ent-Base</c> when it stands under
/// <c>ent-Base.desc</c>. That is the whole of inheritance in a language: a child whose description is the description of its
/// kind points at it rather than holding a copy that goes stale.
/// </para>
/// <para>
/// A placeholder of a text is written without the spaces, <c>{count}</c>, so the two never mean the same thing. A reference that
/// is written like a placeholder is a mistake a person makes, and the service answers with the text it has and says so once in
/// the log rather than showing an empty line.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ent-Goblin: goblin
/// ent-Goblin.desc: A small, mean creature.
/// ent-GoblinHeavy: "{ ent-Goblin }"
/// ent-GoblinHeavy.desc: "{ ent-Goblin.desc }"
/// </code>
/// </example>
public static class LocaleReference
{
    /// <summary>Returns the key a text refers to, or null when the text is a text of its own.</summary>
    /// <param name="text">The text a document wrote under a key, its attribute or its form.</param>
    /// <returns>The key inside the braces, or null when the text is not a reference.</returns>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    public static string? TryKey(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length < 5 || !text.StartsWith("{ ", StringComparison.Ordinal) || !text.EndsWith(" }", StringComparison.Ordinal))
        {
            return null;
        }

        string key = text[2..^2].Trim();

        return key.Length > 0 && !key.Contains('{') && !key.Contains('}') ? key : null;
    }
}
