using System.Text.Json;

namespace Age.Core;

/// <summary>
/// What a scene needs from the content of a game: the components that a prototype declares, and a way to make an entity
/// out of one.
/// </summary>
/// <remarks>
/// <para>
/// A scene writes the prototype that an entity was created from and only the components that differ from it, so reading
/// such a scene needs the content the prototype belongs to. The core holds no content of its own, so the content
/// assembly implements this and the serializer takes it when the container has it.
/// </para>
/// <para>
/// A scene whose entities carry every component they need is still read without any content: a prototype is only named
/// by a scene that was written from entities of one.
/// </para>
/// </remarks>
public interface IPrototypeSource
{
    /// <summary>Creates an entity in a world that carries the components that a prototype declares.</summary>
    /// <param name="world">The world to create the entity in.</param>
    /// <param name="prototypeId">The identifier of the prototype the entity is made of.</param>
    /// <returns>The entity, which carries what the prototype declares.</returns>
    /// <exception cref="KeyNotFoundException">The content holds no prototype under that identifier.</exception>
    Entity Spawn(World world, string prototypeId);

    /// <summary>Returns the components that a prototype declares, keyed by the name they are registered under.</summary>
    /// <param name="prototypeId">The identifier of the prototype.</param>
    /// <returns>The components and the values they start with, or null when the content holds no such prototype.</returns>
    /// <remarks>The serializer compares what an entity holds with what its prototype declares, which is how a scene keeps only the differences.</remarks>
    IReadOnlyDictionary<string, JsonElement>? ComponentsOf(string prototypeId);
}
