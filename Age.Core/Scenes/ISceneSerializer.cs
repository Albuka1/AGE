namespace Age.Core;

/// <summary>
/// Writes the entities and components of a world as a scene, and reads them back.
/// </summary>
/// <example>
/// <code>
/// ISceneSerializer scenes = provider.GetRequiredService&lt;ISceneSerializer&gt;();
///
/// string json = scenes.Save(world);
/// scenes.Load(world, json);
/// </code>
/// </example>
/// <remarks>
/// The component types that a scene can hold come from the <see cref="ComponentRegistry"/>, which the assemblies of the
/// engine and the game fill through <see cref="IComponentRegistrations"/>.
/// </remarks>
public interface ISceneSerializer
{
    /// <summary>Writes the entities and components of a world as JSON text.</summary>
    /// <param name="world">The world to write.</param>
    /// <returns>The scene, formatted so that it can be read and edited by hand.</returns>
    /// <exception cref="ArgumentNullException">The world is null.</exception>
    string Save(World world);

    /// <summary>Adds the entities and components of a scene to a world.</summary>
    /// <param name="world">The world to add the entities to. The entities it already holds stay.</param>
    /// <param name="json">The scene text that <see cref="Save"/> produced.</param>
    /// <exception cref="ArgumentNullException">The world is null.</exception>
    /// <exception cref="ArgumentException">The text is null, empty or whitespace.</exception>
    /// <exception cref="InvalidDataException">The text is not a scene, it holds a component that is not registered, or it was written in a version this build does not read.</exception>
    /// <remarks>
    /// <para>
    /// Every entity of the scene becomes a new entity, so the slots of the loaded world differ from the ones the scene
    /// was saved from. The identifiers of the scene survive: each one maps to the entity that took it, so a component
    /// that refers to another entity through <see cref="EntityRef"/> reads back as the same entity.
    /// </para>
    /// <para>
    /// The identifiers of the scene win over the ones a world assigned on its own, so an entity the world already holds
    /// that uses one of them is given a fresh identifier. A reference that a component of that world already holds to
    /// such an entity does not follow it, which is why a scene is best loaded into a world of its own: a game that keeps
    /// entities across a load either writes them into the same scene or keeps its own way of finding them.
    /// </para>
    /// <para>
    /// An entity that was saved from a prototype is made again through the content of the game, so the container has to
    /// hold the prototypes the scene names: <c>AddAgeContent</c> registers them, and a scene that names one without any
    /// content is refused rather than loaded empty.
    /// </para>
    /// </remarks>
    void Load(World world, string json);
}
