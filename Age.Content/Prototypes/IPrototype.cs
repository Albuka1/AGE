namespace Age.Content.Prototypes;

/// <summary>
/// Marks a type as the data of a prototype: what a document declares under an identifier, and what the manager holds.
/// </summary>
/// <remarks>
/// A prototype is data rather than a class: an enemy, a weapon, an item, a recipe or an effect is a document that names
/// the components it carries and the values they start with. A type that implements this interface is the shape of one
/// family of those documents, and the identifier it holds is the name a file and a component refer to it by.
/// </remarks>
public interface IPrototype
{
    /// <summary>Gets the identifier that the document declared the prototype under.</summary>
    string Id { get; }
}
