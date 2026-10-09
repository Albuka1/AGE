using System.Diagnostics.CodeAnalysis;

namespace Age.Content.Prototypes;

/// <summary>
/// The data of an entity of a game: what a document of the kind <c>entity</c> declares, read as the thing a spawn makes.
/// </summary>
/// <remarks>
/// This is the kind that turns content into units: a map, an enemy or an item is a document that names the components an
/// entity of that kind carries and the values they start with, and a spawn creates an entity and attaches exactly those
/// components. A game registers this kind once, which is what binds the word <c>entity</c> of a document to the entity of
/// a world, and then everything else of its content is data.
/// </remarks>
/// <example>
/// <code>
/// prototypes.Register(EntityPrototype.Kind, EntityPrototype.Read);
/// </code>
/// </example>
public sealed class EntityPrototype : IPrototype
{
    /// <summary>The word that a document writes in its <c>type</c> field to declare an entity.</summary>
    public const string Kind = "entity";

    /// <summary>Initializes the data of an entity from what a document declared.</summary>
    /// <param name="data">The prototype that the document declared.</param>
    /// <exception cref="ArgumentNullException">The data is null.</exception>
    public EntityPrototype(Prototype data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Data = data;
    }

    /// <summary>Gets the prototype as the document declared it, which is where its components and their values live.</summary>
    public Prototype Data { get; }

    /// <inheritdoc />
    public string Id => Data.Id;

    /// <summary>Gets the components of the entity and the values they start with, in the order the documents declared them.</summary>
    public IReadOnlyList<PrototypeComponent> Components => Data.Components;

    /// <summary>Determines whether the entity carries a component.</summary>
    /// <param name="name">The name a document uses for the component.</param>
    /// <returns><see langword="true"/> when the entity carries it.</returns>
    public bool Has(string name) => Data.Has(name);

    /// <summary>Returns the values of one component of the entity.</summary>
    /// <param name="name">The name a document uses for the component.</param>
    /// <param name="component">Receives the component and its values.</param>
    /// <returns><see langword="true"/> when the entity carries the component.</returns>
    public bool TryGet(string name, [NotNullWhen(true)] out PrototypeComponent? component) => Data.TryGet(name, out component);

    /// <summary>Reads the data of an entity, which is what a game registers as the kind <see cref="Kind"/>.</summary>
    /// <param name="data">The prototype that a document declared.</param>
    /// <returns>The data of the entity.</returns>
    public static EntityPrototype Read(Prototype data) => new(data);

    /// <inheritdoc />
    public override string ToString() => $"entity '{Id}'";
}
