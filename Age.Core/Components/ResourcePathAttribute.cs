namespace Age.Core;

/// <summary>
/// Marks the field of a component that holds the path of a resource, which is what a tool checks against the files a build ships.
/// </summary>
/// <remarks>
/// <para>
/// A path that content writes is the mistake a reader of content cannot catch on its own: the value is a well-formed word
/// whether or not a file stands behind it, so a linter has to ask the game root. A field that carries this attribute is
/// what <c>Age.Content.Lint</c> looks at, and a path whose file is not there fails a build rather than becoming a
/// placeholder in a fight.
/// </para>
/// <para>
/// A resource that content names is named by path rather than by handle, because a handle belongs to the run that made it:
/// the sprite of the engine is the one case of that today, and a component of a game follows the same rule.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [ResourcePath]
/// public string? TexturePath;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ResourcePathAttribute : Attribute;
