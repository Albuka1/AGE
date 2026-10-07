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
    /// <exception cref="InvalidDataException">The text is not a scene, or it holds a component that is not registered.</exception>
    /// <remarks>
    /// Every entity of the scene becomes a new entity, so the identifiers in the loaded world differ from the ones the
    /// scene was saved from.
    /// </remarks>
    void Load(World world, string json);
}
