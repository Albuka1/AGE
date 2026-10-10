using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Age.Content.Prototypes;

/// <summary>
/// One prototype as a document declared it: the identifier it is known by, the kind of data it is, and the components it
/// holds with the values they start with.
/// </summary>
/// <remarks>
/// A prototype is the data of a thing rather than the thing: a weapon, an enemy, a recipe or an effect is a document that
/// says which components it carries, and the components themselves come from the engine. That is what lets a new kind of
/// enemy be a file rather than a class, and what keeps a change to the data of a hundred of them a change in one place.
/// </remarks>
public sealed class Prototype : IPrototype
{
    /// <summary>Initializes a prototype.</summary>
    /// <param name="id">The identifier the document declared.</param>
    /// <param name="kind">The kind of data, which is what the <c>type</c> field of a document says.</param>
    /// <param name="parent">The identifier of the prototype this one inherits from, or null when it inherits from none.</param>
    /// <param name="nameKey">The key of the string that names this prototype, or null when the document did not write one.</param>
    /// <param name="descKey">The key of the string that describes this prototype, or null when the document did not write one.</param>
    /// <param name="file">The file the prototype was read from, which an error mentions.</param>
    /// <param name="line">The line of the file, counting from one.</param>
    /// <param name="components">The components and their values, in the order the documents declared them.</param>
    /// <param name="fields">The fields of the document that are not one of the reserved ones, in the order they were written.</param>
    public Prototype(string id, string kind, string? parent, string? nameKey, string? descKey, string file, int line, IReadOnlyList<PrototypeComponent> components, IReadOnlyList<PrototypeComponent>? fields = null)
    {
        Id = id;
        Kind = kind;
        Parent = parent;
        NameKey = nameKey;
        DescKey = descKey;
        File = file;
        Line = line;
        Components = components;
        Fields = fields ?? [];
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <summary>Gets the kind of data the prototype holds, which is what a document writes in its <c>type</c> field.</summary>
    public string Kind { get; }

    /// <summary>Gets the identifier of the prototype this one inherits from, or null when it inherits from none.</summary>
    public string? Parent { get; }

    /// <summary>
    /// Gets the key of the string that names this prototype, or null when neither the document nor its parent wrote one.
    /// </summary>
    /// <remarks>
    /// A name is a key rather than a text, because the text of a name belongs to a language: what a document writes is where
    /// to look for it, and a game that has no string under that key draws the identifier instead of nothing at all.
    /// </remarks>
    public string? NameKey { get; }

    /// <summary>Gets the key of the string that describes this prototype, or null when neither the document nor its parent wrote one.</summary>
    public string? DescKey { get; }

    /// <summary>Gets the file the prototype was read from.</summary>
    public string File { get; }

    /// <summary>Gets the line of the file that declared the prototype, counting from one.</summary>
    public int Line { get; }

    /// <summary>Gets the components and the values they start with, in the order the documents declared them.</summary>
    public IReadOnlyList<PrototypeComponent> Components { get; }

    /// <summary>Gets the fields of the document that are not one of the reserved ones, in the order they were written.</summary>
    /// <remarks>
    /// A document of a kind that is not a thing — a material, a recipe, a faction — writes its own fields, and the manager carries
    /// them the way it carries a component so that the kind reads one name, one file and one line wherever it looks. A field of a
    /// game is data rather than state of a run, so it travels with the identifier the same way the components do.
    /// </remarks>
    public IReadOnlyList<PrototypeComponent> Fields { get; }

    /// <summary>Determines whether the prototype carries a component.</summary>
    /// <param name="name">The name a document uses for the component.</param>
    /// <returns><see langword="true"/> when the prototype carries it.</returns>
    public bool Has(string name) => TryGet(name, out _);

    /// <summary>Returns the values of one component of the prototype.</summary>
    /// <param name="name">The name a document uses for the component.</param>
    /// <param name="component">Receives the component and its values.</param>
    /// <returns><see langword="true"/> when the prototype carries the component.</returns>
    public bool TryGet(string name, [NotNullWhen(true)] out PrototypeComponent? component)
    {
        ArgumentNullException.ThrowIfNull(name);

        foreach (PrototypeComponent candidate in Components)
        {
            if (string.Equals(candidate.Name, name, StringComparison.Ordinal))
            {
                component = candidate;
                return true;
            }
        }

        component = null;
        return false;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Kind} '{Id}'";
}

/// <summary>
/// The values that one component of a prototype starts with, written the way a scene writes them.
/// </summary>
/// <param name="Name">The name the component is registered under, such as <c>Transform</c>.</param>
/// <param name="Values">The values of the component, which the contract of that component reads.</param>
/// <param name="File">The file the values came from, which is the file of a parent when they were inherited.</param>
/// <param name="Line">The line of the file, counting from one.</param>
public sealed record PrototypeComponent(string Name, JsonElement Values, string File, int Line)
{
    /// <inheritdoc />
    public override string ToString() => $"{Name} ({File}:{Line})";
}
