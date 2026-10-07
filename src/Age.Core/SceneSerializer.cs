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

        var scene = new SceneData();
        var entities = new Dictionary<int, SceneEntity>();

        foreach (ComponentRegistration registration in _components.Registrations)
        {
            foreach (Entity entity in registration.Entities(world))
            {
                if (!entities.TryGetValue(entity.Id, out SceneEntity? saved))
                {
                    saved = new SceneEntity();
                    entities[entity.Id] = saved;
                    scene.Entities.Add(saved);
                }

                saved.Components[registration.Name] = registration.Serialize(world, entity);
            }
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

        foreach (SceneEntity saved in scene.Entities ?? [])
        {
            Entity entity = world.CreateEntity();

            foreach ((string name, JsonElement component) in saved.Components ?? [])
            {
                if (!_components.TryGet(name, out ComponentRegistration? registration))
                {
                    throw new InvalidDataException($"The scene holds a component named '{name}', which is not registered. Register it before loading the scene.");
                }

                registration.Deserialize(world, entity, component);
            }
        }
    }
}
