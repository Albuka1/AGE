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
    /// <remarks>
    /// <para>
    /// Text without a version counts as this one, so a scene that was written by hand reads without it.
    /// </para>
    /// <para>
    /// Version two gave <see cref="SceneEntity.Prototype"/> its meaning: an entity of a prototype is written as that
    /// reference and only as the components that differ from it, so a build that reads version one would apply no
    /// components to such an entity and make it empty. A scene of version one still reads as it did, because a version
    /// one scene carries every component of every entity.
    /// </para>
    /// </remarks>
    public const int CurrentVersion = 2;

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

    /// <summary>Gets or sets the prototype the entity was created from, or null when the scene carries every component of it.</summary>
    /// <remarks>
    /// <para>
    /// An entity that a spawn made is written as this reference and as the components that differ from what the prototype
    /// declares, which is what keeps a map of a hundred units of one kind from repeating the same components a hundred
    /// times. Reading the scene makes the entity again through the content of the game, so a scene that names a prototype
    /// needs the content that declares it.
    /// </para>
    /// <para>
    /// A scene has no way to say that an entity lost a component that its prototype declares, so such an entity is
    /// written with every component and without a reference.
    /// </para>
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
