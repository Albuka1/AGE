namespace Age.Core;

/// <summary>
/// Marks the member of a component that names a state of a sprite sheet, which is what a tool checks against the sheet that
/// another member of the same component names.
/// </summary>
/// <remarks>
/// <para>
/// A state is a name rather than an identifier: the document of a sheet declares the states a game may play, and a prototype
/// that asks for one the sheet does not have draws the placeholder and hears about it once in the log. That is late for a
/// mistake a build can see, so the member that names a sheet and the member that names a state of it point at each other
/// here, and <c>Age.Content.Lint</c> reads the document of the sheet and refuses a state it does not declare.
/// </para>
/// <para>
/// The sheet is named by a member of the same component that holds its path, which is a member that carries
/// <see cref="ResourcePathAttribute"/>: a state on its own says nothing about which sheet it belongs to.
/// </para>
/// <para>
/// A component that plays a state of the sheet that <em>another</em> component of the same document names gives that
/// component and that member, which is what an animation of a sprite does: the sheet is the one the sprite names, and a
/// state of the animation is a state of it all the same.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [ResourcePath]
/// public string? SheetPath;
///
/// [SheetState(nameof(SheetPath))]
/// public string? State;
///
/// // In another component of the same document:
/// [SheetState(typeof(SpriteComponent), nameof(SpriteComponent.SheetPath))]
/// public string? Played;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class SheetStateAttribute : Attribute
{
    /// <summary>Initializes the attribute with the name of the member of the same component that holds the path of the sheet.</summary>
    /// <param name="sheet">The name of the field or property of the same component that holds the path of the document of the sheet.</param>
    /// <exception cref="ArgumentException">The name is null, empty or whitespace.</exception>
    public SheetStateAttribute(string sheet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sheet);
        Sheet = sheet;
    }

    /// <summary>Initializes the attribute with the component and the member that hold the path of the sheet.</summary>
    /// <param name="component">The type of the component of the same document that holds the path of the sheet.</param>
    /// <param name="sheet">The name of the field or property of that component that holds the path of the document of the sheet.</param>
    /// <exception cref="ArgumentNullException">The component type is null.</exception>
    /// <exception cref="ArgumentException">The name is null, empty or whitespace.</exception>
    public SheetStateAttribute(Type component, string sheet)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentException.ThrowIfNullOrWhiteSpace(sheet);
        Component = component;
        Sheet = sheet;
    }

    /// <summary>Gets the type of the component that holds the path of the sheet, or null when it is the component of the member itself.</summary>
    public Type? Component { get; }

    /// <summary>Gets the name of the member of that component that holds the path of the sheet.</summary>
    public string Sheet { get; }
}
