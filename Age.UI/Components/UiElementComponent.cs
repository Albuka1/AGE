using Age.Core;

namespace Age.UI;

/// <summary>
/// Names an element of an interface inside the window that built it, which is what a window reads back to update what it
/// made.
/// </summary>
/// <remarks>
/// The tree of the interface is the one every element already has: a child names its parent in
/// <see cref="ParentComponent"/> and a parent lists its children in <see cref="ChildrenComponent"/>. What this component
/// adds is the name a window wrote when it built the element and reads when it updates it, so a window does not hold an
/// identifier of an entity between two frames, and a name is unique inside one window rather than across a world: two
/// windows that both hold a "close" button are two different buttons.
/// </remarks>
[Component("UiElement")]
public struct UiElementComponent : IComponent
{
    /// <summary>Gets or sets the name of the element inside the window that built it.</summary>
    public string Id;
}
