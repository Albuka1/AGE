namespace Age.Content.Prototypes;

/// <summary>
/// Marks a class as the data of one kind of prototype, which is what lets the content of a game be read without a list
/// of the kinds it holds.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PrototypeKindAttribute : Attribute
{
    /// <summary>Initializes the attribute with the word that a document writes in its <c>type</c> field.</summary>
    /// <param name="kind">The word a document writes, which is the identifier the kind is known by.</param>
    /// <exception cref="ArgumentException">The word is null, empty or whitespace.</exception>
    public PrototypeKindAttribute(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        Kind = kind;
    }

    /// <summary>Gets the word that a document writes in its <c>type</c> field to declare a prototype of this kind.</summary>
    public string Kind { get; }
}
