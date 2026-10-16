namespace Age.Content.Prototypes;

/// <summary>
/// Marks a class as the data of one kind of prototype, which is what lets the content of a game be read without a list
/// of the kinds it holds.
/// </summary>
/// <remarks>
/// <para>
/// The class has to implement <see cref="IPrototype"/> and to declare a static method <c>Read(Prototype data)</c> that
/// answers an <see cref="IPrototype"/>, because a kind is a word of a document and the method that reads it: the word
/// is the attribute, the method is what the manager calls, and the type of the answer is what a game asks for through
/// <see cref="IPrototypeManager.TryGet{T}"/>.
/// </para>
/// <para>
/// The word is written once, in the attribute, and the manager holds it: a document that names the kind resolves to
/// this class, and the class is what the manager calls.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [PrototypeKind(WeaponPrototype.Kind)]
/// public sealed class WeaponPrototype : IPrototype
/// {
///     public const string Kind = "weapon";
///
///     public static IPrototype Read(Prototype data) { ... }
/// }
/// </code>
/// </example>
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