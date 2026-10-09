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
/// The sheet is named by the member of the same component that holds its path, which is a member that carries
/// <see cref="ResourcePathAttribute"/>: a state on its own says nothing about which sheet it belongs to.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [ResourcePath]
/// public string? SheetPath;
///
/// [SheetState(nameof(SheetPath))]
/// public string? State;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class SheetStateAttribute : Attribute
{
    /// <summary>Initializes the attribute with the name of the member that holds the path of the sheet.</summary>
    /// <param name="sheet">The name of the field or property of the same component that holds the path of the document of the sheet.</param>
    /// <exception cref="ArgumentException">The name is null, empty or whitespace.</exception>
    public SheetStateAttribute(string sheet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sheet);
        Sheet = sheet;
    }

    /// <summary>Gets the name of the member of the same component that holds the path of the sheet.</summary>
    public string Sheet { get; }
}
