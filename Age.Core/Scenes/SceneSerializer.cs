using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Age.Core;

/// <summary>
/// The default <see cref="ISceneSerializer"/>. It walks the registrations of the <see cref="ComponentRegistry"/> and
/// reads and writes every component through the contract that was registered with it.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here goes through reflection: the contracts come from the source generated contexts of the assemblies, and
/// the scene itself is written by the context of this assembly, so an AOT build keeps working.
/// </para>
/// <para>
/// An entity that a spawn made is written as the prototype it was created from, together with the components that differ
/// from what that prototype declares, and reading such a scene makes the entities again through the
/// <see cref="IPrototypeSource"/> of the container. That is what keeps a map from repeating the components of one kind of
/// creature for every creature on it.
/// </para>
/// </remarks>
public sealed class SceneSerializer : ISceneSerializer
{
    private readonly ComponentRegistry _components;
    private readonly ILogger<SceneSerializer>? _logger;
    private readonly IPrototypeSource? _prototypes;

    /// <summary>Initializes the serializer with the registry that names the components.</summary>
    /// <param name="components">The registry of the component types that a scene can hold.</param>
    /// <param name="logger">The logger that reports a scene which was refused, or null to report nothing.</param>
    /// <param name="prototypes">The content of the game, which a scene that stores prototypes needs, or null when there is none.</param>
    /// <exception cref="ArgumentNullException">The registry is null.</exception>
    public SceneSerializer(ComponentRegistry components, ILogger<SceneSerializer>? logger = null, IPrototypeSource? prototypes = null)
    {
        ArgumentNullException.ThrowIfNull(components);
        _components = components;
        _logger = logger;
        _prototypes = prototypes;
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

            // What the entity owes to its prototype is not written a second time: the prototype is named and the scene
            // keeps only what differs from it. An entity that lost a component that its prototype declares is written in
            // full instead, because a scene has no way to say "without the component of the prototype", and so is one whose
            // prototype this build does not hold: a reference that nothing resolves is a scene that cannot be read back.
            if (PrototypeOf(world, entity, saved.Components) is string prototypeId)
            {
                saved.Prototype = prototypeId;
            }

            scene.Entities.Add(saved);
        }

        return JsonSerializer.Serialize(scene, AgeJsonContext.Default.SceneData);
    }

    /// <summary>Returns the prototype to name for an entity, and drops from its components what the prototype already says.</summary>
    /// <param name="world">The world that holds the entity.</param>
    /// <param name="entity">The entity that is being written.</param>
    /// <param name="components">The components of the entity, which loses the ones that the prototype already declares.</param>
    /// <returns>The identifier of the prototype, or null when the entity is written without one.</returns>
    /// <remarks>
    /// The values of a prototype are read through the contract of the component before they are compared, so the fields
    /// that its document leaves out count as the values the component starts with and the order of the fields does not
    /// matter. A prototype that this build does not hold is not named at all: a reference that nothing resolves is a scene
    /// that the reading refuses, and a file this build writes has to be a file this build reads, so the entity is written
    /// with every component it holds instead.
    /// </remarks>
    private string? PrototypeOf(World world, Entity entity, Dictionary<string, JsonElement> components)
    {
        if (_prototypes is null || world.PrototypeOf(entity) is not string prototypeId)
        {
            return null;
        }

        if (_prototypes.ComponentsOf(prototypeId) is not IReadOnlyDictionary<string, JsonElement> declared)
        {
            return null;
        }

        // What the prototype already says is gathered as it is read and written into the scene only once the whole of it was read:
        // an entity that lost a component of its prototype, or one whose component cannot be read, is written in full, and a scene
        // that is written in full keeps every component it holds rather than the ones that happened to be pruned before the walk
        // gave up.
        List<string>? matches = null;
        List<(string Name, JsonElement Values)>? pruned = null;

        foreach ((string name, JsonElement values) in declared)
        {
            if (!components.TryGetValue(name, out JsonElement component) || !_components.TryGet(name, out ComponentRegistration? registration))
            {
                // The entity does not hold a component of its prototype any more, and the scene cannot say that.
                return null;
            }

            JsonElement expected;
            try
            {
                expected = registration.Normalize(values);
            }
            catch (JsonException)
            {
                // A prototype whose values cannot be read is content that a build of a content linter refuses: nothing can
                // be left out of the scene for it, so the entity is written in full.
                return null;
            }

            // A component that matches its prototype in every field is dropped whole. One that differs in some fields keeps only
            // the fields that differ, so a sprite that changed its state does not carry the image, the size and the colour of its
            // prototype a second time. What is left is written over the entity the prototype makes when the scene is read back.
            if (JsonElement.DeepEquals(component, expected))
            {
                (matches ??= []).Add(name);
                continue;
            }

            if (Prune(component, expected) is JsonElement replacement)
            {
                (pruned ??= []).Add((name, replacement));
            }
        }

        // Every component of the prototype was read, so the scene now takes the shape it was gathered in.
        if (pruned is not null)
        {
            foreach ((string name, JsonElement values) in pruned)
            {
                components[name] = values;
            }
        }

        if (matches is not null)
        {
            foreach (string name in matches)
            {
                components.Remove(name);
            }
        }

        return prototypeId;
    }

    /// <summary>Returns a component with every field that the prototype already declares removed, or null when nothing can be removed.</summary>
    /// <param name="component">The component as the entity carries it, written as a JSON object.</param>
    /// <param name="prototype">The same component as the prototype declares it, in the same shape.</param>
    /// <returns>The component with the fields that match the prototype left out, or null when the two do not share the shape a per-field comparison needs.</returns>
    /// <remarks>
    /// The comparison is a walk of the two objects rather than of the text, so the order of the fields does not matter and a value that
    /// the prototype writes differently — an int where the component holds a float — is compared as the component reads it, which is
    /// what <see cref="ComponentRegistration.Normalize"/> gives. A component that is not an object keeps whole: a field of a component
    /// is what a document may leave out, and a value that is not an object has no fields to leave out.
    /// </remarks>
    private static JsonElement? Prune(JsonElement component, JsonElement prototype)
    {
        if (component.ValueKind != JsonValueKind.Object || prototype.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var kept = new List<(string Name, JsonElement Value)>();

        foreach (JsonProperty field in component.EnumerateObject())
        {
            if (prototype.TryGetProperty(field.Name, out JsonElement declared) && JsonElement.DeepEquals(field.Value, declared))
            {
                continue;
            }

            kept.Add((field.Name, field.Value));
        }

        // A component whose every field was removed is one the prototype already declares, which the caller drops whole rather than
        // writing an object with nothing in it.
        if (kept.Count == 0 || kept.Count == component.EnumerateObject().Count())
        {
            return null;
        }

        return JsonSerializer.SerializeToElement(
            kept.ToDictionary(field => field.Name, field => field.Value),
            AgeJsonContext.Default.Options);
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
        var checkedPrototypes = new HashSet<string>(StringComparer.Ordinal);

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

            var components = new List<(ComponentRegistration Registration, JsonElement Values)>(saved.Components.Count);

            foreach ((string name, JsonElement component) in saved.Components)
            {
                if (!_components.TryGet(name, out ComponentRegistration? registration))
                {
                    throw new InvalidDataException($"The scene holds a component named '{name}', which is not registered. Register it before loading the scene.");
                }

                // The values are kept as they were written rather than read into a component here, because how they are written over
                // depends on the entity: over the component a prototype made, a field the scene leaves out keeps what the prototype gave
                // it, and that is a decision the world makes when the entity is there. Reading them once here still refuses a value this
                // build cannot read, so a scene that is refused leaves the world alone.
                try
                {
                    registration.Deserialize(component);
                }
                catch (JsonException exception)
                {
                    throw new InvalidDataException($"The component '{name}' of the scene cannot be read.", exception);
                }

                components.Add((registration, component));
            }

            // The prototype of an entity is checked here, before the world is touched: a scene that names one needs the content
            // that declares it, and every component of that content has to be one this build can read, so that making the
            // entity cannot fail half way through the scene. One prototype is checked once however many entities it makes.
            if (saved.Prototype is string prototypeId)
            {
                if (string.IsNullOrWhiteSpace(prototypeId))
                {
                    throw new InvalidDataException("A scene entity names an empty prototype, and a prototype is named by its identifier.");
                }

                if (checkedPrototypes.Add(prototypeId))
                {
                    CheckPrototype(prototypeId);
                }
            }

            staged.Add(new StagedEntity(saved.Id, saved.Prototype, components));
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
            // An entity that was saved from a prototype is made by the content again, and the scene lays its own
            // components over it, which are exactly the ones that differed from the prototype. Staging refused a scene
            // that names a prototype no content can supply, so the source is here.
            Entity entity = stagedEntity.Prototype is string prototypeId
                ? _prototypes!.Spawn(world, prototypeId)
                : world.CreateEntity();

            if (stagedEntity.Id != 0)
            {
                world.MapSceneId(entity, stagedEntity.Id);
            }

            foreach ((ComponentRegistration registration, JsonElement values) in stagedEntity.Components)
            {
                // The values are written over the component the entity already carries, so a field the scene left out keeps what the
                // prototype gave it; an entity that names no prototype carries nothing yet, so the whole value lands.
                registration.Merge(world, entity, values);
            }
        }
    }

    /// <summary>Checks that the content of a build can make an entity of a prototype, which is what keeps a refused load from touching the world.</summary>
    /// <param name="prototypeId">The identifier of the prototype that a scene names.</param>
    /// <exception cref="InvalidDataException">
    /// No content is loaded, the content holds no such prototype, a component that it declares is not registered, or the values
    /// of one of its components cannot be read.
    /// </exception>
    /// <remarks>
    /// Every component that a prototype declares is read through the contract of the component here, which is the same call
    /// that the spawn which applies it makes: content that this build cannot read — a component that nothing registers, values
    /// that its contract refuses, or a field that the format of the component does not carry — is a scene that is refused
    /// rather than a load that stops with half of its entities in the world.
    /// </remarks>
    private void CheckPrototype(string prototypeId)
    {
        if (_prototypes is null)
        {
            throw new InvalidDataException(
                $"The scene holds an entity of the prototype '{prototypeId}' and no content is loaded, so the entity cannot be made. Load the content of the game before the scene.");
        }

        if (_prototypes.ComponentsOf(prototypeId) is not IReadOnlyDictionary<string, JsonElement> declared)
        {
            throw new InvalidDataException(
                $"The scene names the prototype '{prototypeId}', and the content of the game does not hold it. Add the document that declares it, or write the entity with every component it needs.");
        }

        foreach ((string name, JsonElement values) in declared)
        {
            if (!_components.TryGet(name, out ComponentRegistration? registration))
            {
                throw new InvalidDataException(
                    $"The prototype '{prototypeId}' of the scene declares the component '{name}', and nothing registers a component under that name, so the entity cannot be made. Register it before loading the scene.");
            }

            try
            {
                registration.Deserialize(values);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    $"The component '{name}' of the prototype '{prototypeId}' cannot be read by this build, so the entity cannot be made.",
                    exception);
            }
        }
    }


    /// <summary>One entity of a scene that was read and checked, and is about to be applied to a world.</summary>
    /// <param name="Id">The identifier the scene knows the entity by, or zero when it carried none.</param>
    /// <param name="Prototype">The prototype the entity was created from, or null when the scene carries every component of it.</param>
    /// <param name="Components">The components of the entity, in the order the scene listed them.</param>
    private sealed record StagedEntity(int Id, string? Prototype, List<(ComponentRegistration Registration, JsonElement Values)> Components);
}
