using System.Text.Json;

namespace Age.Core;

/// <summary>
/// The entities and components of a world, in the shape that a scene file holds them.
/// </summary>
/// <example>
/// <code>
/// string json = scenes.Save(world);
/// scenes.Load(other, json);
/// </code>
/// </example>
/// <remarks>
/// A scene stores the identifier that a world knows an entity by, so a component that refers to another entity is
/// written as that number and reads back the same way. Loading a scene creates new entities, so the slot identifiers of
/// the loaded world differ from the ones the scene was saved from: the identifiers of the scene are the ones that
/// survive, and <see cref="World.SceneIdOf"/> reports the one an entity carries now. The components of an entity are
/// keyed by the name they were registered under, so a file stays readable and can be edited by hand.
/// </remarks>
public sealed class SceneData
{
    /// <summary>The version of the format that this build writes, which <see cref="Version"/> carries.</summary>
    /// <remarks>Text without a version counts as this one, so a scene that was written by hand reads without it.</remarks>
    public const int CurrentVersion = 1;

    /// <summary>Gets or sets the version of the format that the text was written in.</summary>
    /// <remarks>
    /// A version larger than <see cref="CurrentVersion"/> is refused when the scene is read, because the text holds a
    /// shape this build does not know. Leaving the property out, or writing zero, means the current version.
    /// </remarks>
    public int Version { get; set; }

    /// <summary>Gets or sets the entities of the scene, in the order they were saved.</summary>
    /// <remarks>A null value means that the text carried no entities list at all, which the serializer refuses to load.</remarks>
    public List<SceneEntity>? Entities { get; set; }
}

/// <summary>
/// One entity of a <see cref="SceneData"/>, as its identifier and the components it carries.
/// </summary>
public sealed class SceneEntity
{
    /// <summary>Gets or sets the identifier that the scene knows this entity by.</summary>
    /// <remarks>
    /// A component that refers to another entity holds this number through <see cref="EntityRef"/>, so the reference
    /// survives a save and a load. <see cref="World.SceneIdOf"/> reports it for an entity of a world, and a load maps
    /// the number to the entity it created. The identifiers of the scene win over the ones a world assigned on its own:
    /// an entity that already uses one of them is given a fresh identifier. Zero means that the loader assigns an
    /// identifier of its own, which is what a scene written by hand can use as long as nothing refers to that entity.
    /// </remarks>
    public int Id { get; set; }

    /// <summary>Gets or sets the prototype the entity was created from. Reserved for a later step.</summary>
    /// <remarks>
    /// The field is written and read, so a scene keeps it, but nothing in this build turns a prototype into an entity
    /// yet: it is where an entity stops repeating the components of its kind and starts referring to them.
    /// </remarks>
    public string? Prototype { get; set; }

    /// <summary>Gets or sets the components of the entity, keyed by their registered name.</summary>
    /// <remarks>
    /// The value of a component stays raw JSON while the scene is read, so the serializer can hand it to the contract of
    /// the registered type and a component the registry does not know about keeps its text. A null value means that the
    /// entry carried no component map, which the serializer refuses to load.
    /// </remarks>
    public Dictionary<string, JsonElement>? Components { get; set; }
}
