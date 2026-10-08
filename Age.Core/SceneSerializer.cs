using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Age.Core;

/// <summary>
/// The default <see cref="ISceneSerializer"/>. It walks the registrations of the <see cref="ComponentRegistry"/> and
/// reads and writes every component through the contract that was registered with it.
/// </summary>
/// <remarks>
/// Nothing here goes through reflection: the contracts come from the source generated contexts of the assemblies, and
/// the scene itself is written by the context of this assembly, so an AOT build keeps working.
/// </remarks>
public sealed class SceneSerializer : ISceneSerializer
{
    private readonly ComponentRegistry _components;
    private readonly ILogger<SceneSerializer>? _logger;

    /// <summary>Initializes the serializer with the registry that names the components.</summary>
    /// <param name="components">The registry of the component types that a scene can hold.</param>
    /// <param name="logger">The logger that reports a scene which was refused, or null to report nothing.</param>
    /// <exception cref="ArgumentNullException">The registry is null.</exception>
    public SceneSerializer(ComponentRegistry components, ILogger<SceneSerializer>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(components);
        _components = components;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Save(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var scene = new SceneData { Version = SceneData.CurrentVersion, Entities = [] };

        // Every entity is written, in the order it was created, so a scene keeps the shape of the world even when an
        // entity carries none of the registered components. The identifier of the scene goes with it, because a
        // component that refers to another entity holds that number.
        foreach (Entity entity in world.Enumerate())
        {
            var saved = new SceneEntity { Id = world.SceneIdOf(entity), Components = [] };

            foreach (ComponentRegistration registration in _components.Registrations)
            {
                if (registration.TrySerialize(world, entity) is JsonElement component)
                {
                    saved.Components[registration.Name] = component;
                }
            }

            scene.Entities.Add(saved);
        }

        return JsonSerializer.Serialize(scene, AgeJsonContext.Default.SceneData);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A scene that is refused leaves a record in the log before the exception reaches the caller, because a file that a
    /// caller was handed is the one thing a person wants to find in the log of a build that failed to load it.
    /// </remarks>
    public void Load(World world, string json)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        try
        {
            LoadCore(world, json);
            _logger?.LogInformation("A scene was applied to a world, which now holds {Entities} entities.", world.Enumerate().Count());
        }
        catch (InvalidDataException exception)
        {
            _logger?.LogError(exception, "A scene was refused: {Reason}", exception.Message);
            throw;
        }
    }

    /// <summary>Reads a scene and applies it, which is what <see cref="Load"/> wraps in its log records.</summary>
    private void LoadCore(World world, string json)
    {
        SceneData scene;
        try
        {
            scene = JsonSerializer.Deserialize(json, AgeJsonContext.Default.SceneData)
                ?? throw new InvalidDataException("The scene text holds no scene.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The scene text is not valid JSON.", exception);
        }

        if (scene.Version > SceneData.CurrentVersion || scene.Version < 0)
        {
            throw new InvalidDataException(
                $"The scene is written in version {scene.Version}, and this build reads version {SceneData.CurrentVersion}. Update the engine, or write the scene again.");
        }

        if (scene.Entities is null)
        {
            throw new InvalidDataException("""The scene text has no entities. A scene without entities is '{ "Entities": [] }'.""");
        }

        // The whole scene is checked and read before the world is touched, so a scene that turns out to be broken leaves
        // the world exactly as it was. An identifier of the scene has to be unique, because a component that refers to an
        // entity by one would otherwise reach the wrong entity. An entity that already holds an identifier of the scene
        // is recorded here and moved aside later: moving it while the scene is still being checked would change a world
        // that a broken scene has to leave alone.
        var staged = new List<StagedEntity>(scene.Entities.Count);
        var identifiers = new HashSet<int>();
        var displaced = new List<Entity>();

        foreach (SceneEntity? saved in scene.Entities)
        {
            if (saved is null)
            {
                throw new InvalidDataException("The scene holds an entry that is not an entity.");
            }

            if (saved.Components is null)
            {
                throw new InvalidDataException("""A scene entity has no components. An entity without components is '{ "Components": {} }'.""");
            }

            if (saved.Id != 0)
            {
                if (saved.Id < 0)
                {
                    throw new InvalidDataException(
                        $"The scene holds the identifier {saved.Id}, and an identifier is a positive number: a world numbers its entities from one.");
                }

                if (saved.Id == int.MaxValue)
                {
                    throw new InvalidDataException(
                        $"The scene holds the identifier {saved.Id}, which leaves no room for the identifiers a world hands out while it applies the scene.");
                }

                if (!identifiers.Add(saved.Id))
                {
                    throw new InvalidDataException(
                        $"The scene holds two entities with the identifier {saved.Id}. An identifier has to be unique, because a component refers to an entity by one.");
                }

                if (world.TryEntityOf(saved.Id, out Entity existing))
                {
                    displaced.Add(existing);
                }
            }

            var components = new List<(ComponentRegistration Registration, object Component)>(saved.Components.Count);

            foreach ((string name, JsonElement component) in saved.Components)
            {
                if (!_components.TryGet(name, out ComponentRegistration? registration))
                {
                    throw new InvalidDataException($"The scene holds a component named '{name}', which is not registered. Register it before loading the scene.");
                }

                object value;
                try
                {
                    value = registration.Deserialize(component);
                }
                catch (JsonException exception)
                {
                    throw new InvalidDataException($"The component '{name}' of the scene cannot be read.", exception);
                }

                components.Add((registration, value));
            }

            staged.Add(new StagedEntity(saved.Id, components));
        }

        // The scene is sound, so the world changes from here on. Every identifier the scene uses is reserved before the
        // first entity is created, and then an entity that already holds one of them moves aside, which gives it an
        // identifier above all of them: a fresh identifier that landed on one the scene is about to map would either
        // throw in the middle of the load or leave two entities behind the same number. A reference that a component of
        // this world already holds to a displaced entity does not follow the move.
        world.ReserveSceneIds(identifiers);

        foreach (Entity existing in displaced)
        {
            world.AssignSceneId(existing);
        }

        foreach (StagedEntity stagedEntity in staged)
        {
            Entity entity = world.CreateEntity();

            if (stagedEntity.Id != 0)
            {
                world.MapSceneId(entity, stagedEntity.Id);
            }

            foreach ((ComponentRegistration registration, object component) in stagedEntity.Components)
            {
                registration.Apply(world, entity, component);
            }
        }
    }

    /// <summary>One entity of a scene that was read and checked, and is about to be applied to a world.</summary>
    /// <param name="Id">The identifier the scene knows the entity by, or zero when it carried none.</param>
    /// <param name="Components">The components of the entity, in the order the scene listed them.</param>
    private sealed record StagedEntity(int Id, List<(ComponentRegistration Registration, object Component)> Components);
}
