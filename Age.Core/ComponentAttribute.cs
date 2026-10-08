namespace Age.Core;

/// <summary>
/// Names the component type as a scene file refers to it, which is what makes the type part of a scene.
/// </summary>
/// <remarks>
/// <para>
/// A component without this attribute is not written to a scene and not read from one: a name is the one declaration that
/// a scene needs, and the registration and the serialization contract of the type come from it rather than from a list
/// that a person has to remember to edit.
/// </para>
/// <para>
/// The name has to be unique across the engine and the game, because a scene that mentions it has to resolve to exactly
/// one component. Renaming a component therefore breaks the scenes that were written with the older name, which is why a
/// name is worth choosing with the same care as a file path.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Component("Transform")]
/// public struct TransformComponent : IComponent
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ComponentAttribute : Attribute
{
    /// <summary>Initializes the attribute with the name that a scene file uses.</summary>
    /// <param name="name">The name of the component, which a scene writes and reads.</param>
    /// <exception cref="ArgumentException">The name is null, empty or whitespace.</exception>
    public ComponentAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>Gets the name of the component.</summary>
    public string Name { get; }
}
