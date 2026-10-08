using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Age.Core;

/// <summary>
/// Names the component types that a scene can hold, so a scene file refers to them by name instead of by type.
/// </summary>
/// <remarks>
/// <para>
/// The registry holds the serialization contract of every component, which a source generated context supplies, so the
/// scene serializer needs no reflection and an AOT build keeps working. Each assembly of the engine registers its own
/// components through <see cref="IComponentRegistrations"/>, and a game registers its own the same way.
/// </para>
/// <para>
/// A name and a type can each be registered once: a second registration of either is an error, because a scene that
/// mentions a name has to resolve to exactly one component.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// registry.Register("Transform", AgeJsonContext.Default.TransformComponent);
///
/// if (registry.TryGetType("Transform", out Type? type))
/// {
///     // type is typeof(TransformComponent)
/// }
/// </code>
/// </example>
public sealed class ComponentRegistry
{
    private readonly Dictionary<string, ComponentRegistration> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, ComponentRegistration> _byType = new();

    /// <summary>Gets the names of the registered components, in the order they were registered.</summary>
    public IEnumerable<string> Names => _byName.Keys;

    /// <summary>Gets the registrations, which is what the scene serializer walks.</summary>
    internal IReadOnlyCollection<ComponentRegistration> Registrations => _byName.Values;

    /// <summary>Registers a component type under the name that a scene file uses for it.</summary>
    /// <typeparam name="T">The component type, which is a struct that implements <see cref="IComponent"/>.</typeparam>
    /// <param name="name">The name to store the component under. It has to be unique.</param>
    /// <param name="typeInfo">The serialization contract of the type, usually from a source generated context.</param>
    /// <exception cref="ArgumentException">The name is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">The contract is null.</exception>
    /// <exception cref="InvalidOperationException">The name or the type is already registered.</exception>
    public void Register<T>(string name, JsonTypeInfo<T> typeInfo) where T : struct, IComponent
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(typeInfo);

        if (_byName.ContainsKey(name))
        {
            throw new InvalidOperationException($"A component is already registered under the name '{name}'.");
        }

        if (_byType.TryGetValue(typeof(T), out ComponentRegistration? existing))
        {
            throw new InvalidOperationException($"The component {typeof(T).Name} is already registered under the name '{existing.Name}'.");
        }

        var registration = new ComponentRegistration(
            name,
            typeof(T),
            (world, entity) => world.Has<T>(entity) ? JsonSerializer.SerializeToElement(world.Get<T>(entity), typeInfo) : null,
            json => JsonSerializer.Deserialize(json, typeInfo)!,
            (world, entity, component) => world.Set(entity, (T)component));

        _byName[name] = registration;
        _byType[typeof(T)] = registration;
    }

    /// <summary>Returns the component type that a name was registered under.</summary>
    /// <param name="name">The name a scene file uses.</param>
    /// <param name="type">Receives the component type when the name is registered.</param>
    /// <returns><see langword="true"/> when the name is registered.</returns>
    public bool TryGetType(string name, [NotNullWhen(true)] out Type? type)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (_byName.TryGetValue(name, out ComponentRegistration? registration))
        {
            type = registration.Type;
            return true;
        }

        type = null;
        return false;
    }

    /// <summary>Returns the registration behind a name, which is how the scene serializer resolves a component.</summary>
    internal bool TryGet(string name, [NotNullWhen(true)] out ComponentRegistration? registration) =>
        _byName.TryGetValue(name, out registration);

    /// <summary>Returns the names the registry holds, which tells a caller whether a name is a component at all.</summary>
    /// <param name="name">The name a document uses for a component.</param>
    /// <returns><see langword="true"/> when the name is registered.</returns>
    public bool Has(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _byName.ContainsKey(name);
    }

    /// <summary>Reads the values of a component from JSON, which is what a prototype does with the values of a document.</summary>
    /// <param name="name">The name the component is registered under.</param>
    /// <param name="values">The values of the component, written the way a scene writes them.</param>
    /// <param name="component">Receives the value that was read, which is of the type the name is registered under.</param>
    /// <returns><see langword="true"/> when the name is registered, <see langword="false"/> when it is not.</returns>
    /// <exception cref="ArgumentNullException">The name is null.</exception>
    /// <exception cref="System.Text.Json.JsonException">The values cannot be read as that component.</exception>
    /// <remarks>
    /// A prototype and a scene use one contract for a component, so a value that a document writes for a prototype is read
    /// by exactly the code that reads a scene, and a field the component does not have is refused by the same call.
    /// </remarks>
    public bool TryDeserialize(string name, JsonElement values, [NotNullWhen(true)] out object? component)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (_byName.TryGetValue(name, out ComponentRegistration? registration))
        {
            component = registration.Deserialize(values);
            return true;
        }

        component = null;
        return false;
    }
}

/// <summary>
/// Everything the scene serializer needs to reach one component type: the way to write it as JSON, to read a value back
/// from JSON, and to apply a value that was read to an entity.
/// </summary>
/// <param name="Name">The name that the component is stored under.</param>
/// <param name="Type">The component type.</param>
/// <param name="TrySerialize">Writes the component of an entity as JSON, or returns null when the entity does not carry it.</param>
/// <param name="Deserialize">Reads a value from JSON, which is what staging a scene does before a world is touched.</param>
/// <param name="Apply">Applies a value that <paramref name="Deserialize"/> read to an entity.</param>
internal sealed record ComponentRegistration(
    string Name,
    Type Type,
    Func<World, Entity, JsonElement?> TrySerialize,
    Func<JsonElement, object> Deserialize,
    Action<World, Entity, object> Apply);
