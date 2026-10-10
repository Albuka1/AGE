using Age.Core;

namespace Age.UI;

/// <summary>
/// Makes a UI element a drop-down: one value shown at a time, and a list of the others that opens over what is under it.
/// </summary>
/// <remarks>
/// <para>
/// The component holds the options and which of them is chosen; the pass that draws the interface draws <see cref="Selected"/> in the
/// closed element and the whole list when <see cref="Open"/>. The list is a value of the component rather than entities of the world, so
/// a drop-down is one element and one component whatever its list holds, and it is the system that reads the pointer of the open list
/// rather than each row being an element of the interface.
/// </para>
/// <para>
/// An open list takes the pointer until it is dismissed, so it is drawn over the elements under it and a click outside it closes it
/// without reaching anything else. <see cref="TextInputSystem"/> is not what dismisses it: the system that reads the pointer for the UI
/// does, so the list closes on the frame the click lands.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// world.Set(choice, new RectTransformComponent { Anchored = true, SizeDelta = new Vector2(160f, 28f), Visible = true });
/// world.Set(choice, new DropdownComponent { Options = ["low", "medium", "high"], Selected = 1, Interactable = true });
/// </code>
/// </example>
[Component("Dropdown")]
public struct DropdownComponent : IComponent
{
    /// <summary>Gets or sets the values the list offers, in the order they are shown.</summary>
    public string[]? Options;

    /// <summary>Gets or sets the index of the chosen value, which is kept inside the list.</summary>
    public int Selected
    {
        readonly get => _selected;
        set => _selected = Options is { Length: > 0 } options ? Math.Clamp(value, 0, options.Length - 1) : 0;
    }

    /// <summary>Gets or sets a value indicating whether the list is open over what is under the element.</summary>
    public bool Open;

    /// <summary>Gets or sets a value indicating whether the drop-down takes input and may be opened by a click.</summary>
    public bool Interactable;

    /// <summary>Gets or sets a value indicating whether the pointer is over the closed element.</summary>
    public bool IsHovered;

    /// <summary>Gets or sets the height of one row of the list, which is what the open list is measured by.</summary>
    public float RowHeight;

    /// <summary>Gets the number of values the list offers.</summary>
    public readonly int Count => Options?.Length ?? 0;

    /// <summary>Gets the chosen value, or an empty string when the list offers none.</summary>
    public readonly string Value => Options is { Length: > 0 } options && _selected >= 0 && _selected < options.Length ? options[_selected] : string.Empty;

    /// <summary>Gets the height of the open list, which is the height of a row for every value it offers.</summary>
    public readonly float ListHeight => Count * RowHeight;

    private int _selected;
}

/// <summary>
/// Raised when the chosen value of a drop-down changes, which is what a game reacts to rather than reading the index on every frame.
/// </summary>
/// <param name="Entity">The element that carries the drop-down.</param>
/// <param name="Index">The index of the value that was chosen.</param>
/// <param name="Value">The value that was chosen.</param>
public readonly record struct DropdownChangedEvent(Entity Entity, int Index, string Value);
