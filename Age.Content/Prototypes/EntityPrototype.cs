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

    /// <summary>Gets the key of the string that names this entity, which is what a game draws.</summary>
    /// <remarks>
    /// A document that writes <c>name</c> names the key itself; one that writes nothing takes the key of its identifier,
    /// <c>ent-&lt;Id&gt;</c>, which is what the documents of a game are expected to hold. A key that no language holds is drawn as
    /// the identifier of the entity before it is drawn as nothing at all.
    /// </remarks>
    public string NameKey => Data.NameKey ?? $"ent-{Id}";

    /// <summary>Gets the key of the string that describes this entity, which is what a game shows on a closer look.</summary>
    public string DescKey => Data.DescKey ?? $"ent-{Id}.desc";

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
    /// <exception cref="ArgumentNullException">The data is null.</exception>
    /// <exception cref="PrototypeException">A field of the document is not one an entity carries.</exception>
    /// <remarks>
    /// An entity carries components and nothing else, so a field a document writes beside them is a mistake of the content: the
    /// manager carries such a field for a kind that declares it, and a kind that does not is what refuses it here. Without this a
    /// document that wrote <c>position: 3</c> where it meant a component would be read and dropped, which is the one thing the
    /// content of the engine never does.
    /// </remarks>
    public static EntityPrototype Read(Prototype data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Fields.Count > 0)
        {
            PrototypeComponent field = data.Fields[0];

            throw new PrototypeException(
                $"{field.File}: the entity '{data.Id}' writes the field '{field.Name}', and an entity carries components rather than fields of its own: what it holds is a list of components under 'components'",
                field.File,
                field.Line);
        }

        return new EntityPrototype(data);
    }

    /// <inheritdoc />
    public override string ToString() => $"entity '{Id}'";
}
