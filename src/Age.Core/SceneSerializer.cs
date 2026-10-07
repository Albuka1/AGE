using System.Text.Json;

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

    /// <summary>Initializes the serializer with the registry that names the components.</summary>
    /// <param name="components">The registry of the component types that a scene can hold.</param>
    /// <exception cref="ArgumentNullException">The registry is null.</exception>
    public SceneSerializer(ComponentRegistry components)
    {
        ArgumentNullException.ThrowIfNull(components);
        _components = components;
    }

    /// <inheritdoc />
    public string Save(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var scene = new SceneData { Entities = [] };

        // Every entity is written, in the order it was created, so a scene keeps the shape of the world even when an
        // entity carries none of the registered components.
        foreach (Entity entity in world.Enumerate())
        {
            var saved = new SceneEntity { Components = [] };

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
    public void Load(World world, string json)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

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

        if (scene.Entities is null)
        {
            throw new InvalidDataException("""The scene text has no entities. A scene without entities is '{ "Entities": [] }'.""");
        }

        // The whole scene is checked and read before the world is touched, so a scene that turns out to be broken leaves
        // the world exactly as it was.
        var staged = new List<List<(ComponentRegistration Registration, object Component)>>(scene.Entities.Count);

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

            staged.Add(components);
        }

        foreach (List<(ComponentRegistration Registration, object Component)> components in staged)
        {
            Entity entity = world.CreateEntity();

            foreach ((ComponentRegistration registration, object component) in components)
            {
                registration.Apply(world, entity, component);
            }
        }
    }
}
