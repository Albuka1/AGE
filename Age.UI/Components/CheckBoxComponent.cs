using Age.Core;

namespace Age.UI;

/// <summary>
/// Makes a UI element a check box: a state the player turns on and off, which a settings screen is made of.
/// </summary>
/// <remarks>
/// <para>
/// The component holds the state and reads the pointer; how the box is drawn is the business of the pass that draws the interface, which
/// reads <see cref="Checked"/>. A press inside the rectangle of the element toggles the state and raises
/// <see cref="CheckChangedEvent"/>, so a game reacts to a change rather than comparing the state with the frame before.
/// </para>
/// <para>
/// The state is a component rather than a field of the button, so an element may be a check box and nothing else. An element that
/// carries both a button and a check box is a button that is a toggle, which is a thing a game sometimes wants.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// world.Set(option, new RectTransformComponent { Anchored = true, SizeDelta = new Vector2(24f, 24f), Visible = true });
/// world.Set(option, new CheckBoxComponent { Checked = true, Interactable = true });
/// </code>
/// </example>
[Component("CheckBox")]
public struct CheckBoxComponent : IComponent
{
    /// <summary>Gets or sets a value indicating whether the box is checked.</summary>
    public bool Checked;

    /// <summary>Gets or sets a value indicating whether the box reacts to input.</summary>
    public bool Interactable;

    /// <summary>Gets or sets a value indicating whether the pointer is currently over the box.</summary>
    public bool IsHovered;
}

/// <summary>
/// Raised when a check box changes its state, which is what a game reacts to rather than comparing the state with the frame before.
/// </summary>
/// <param name="Entity">The element that carries the check box.</param>
/// <param name="Checked">The state the box was turned to.</param>
public readonly record struct CheckChangedEvent(Entity Entity, bool Checked);
