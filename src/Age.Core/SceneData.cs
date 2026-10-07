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
/// A scene stores no entity identifiers: loading one creates new entities. The components of an entity are keyed by the
/// name they were registered under, so a file stays readable and can be edited by hand.
/// </remarks>
public sealed class SceneData
{
    /// <summary>Gets or sets the entities of the scene, in the order they were saved.</summary>
    public List<SceneEntity> Entities { get; set; } = [];
}

/// <summary>
/// One entity of a <see cref="SceneData"/>, as the components it carries.
/// </summary>
public sealed class SceneEntity
{
    /// <summary>Gets or sets the components of the entity, keyed by their registered name.</summary>
    /// <remarks>
    /// The value of a component stays raw JSON while the scene is read, so the serializer can hand it to the contract of
    /// the registered type and a component the registry does not know about keeps its text.
    /// </remarks>
    public Dictionary<string, JsonElement> Components { get; set; } = [];
}
