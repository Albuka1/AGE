using Age.Core;

namespace Age.UI;

/// <summary>
/// Names the element that this one stands inside, which is what makes the interface a tree rather than a flat list.
/// </summary>
/// <remarks>
/// <para>
/// An element that names no parent is a root: it is laid out against the canvas, exactly as every element was before a hierarchy
/// existed. An element that names one is laid out inside the rectangle its parent ended up with, so moving or resizing a panel moves
/// and resizes what it holds, and a scroll of the panel moves the children with it.
/// </para>
/// <para>
/// The reference is an <see cref="EntityRef"/> rather than an <see cref="Entity"/>, so a tree survives a save and a load: the
/// identifier is what a scene knows the parent by, and a loaded world maps it back to the entity it created. A reference to an entity
/// that the world does not hold leaves the element laid out against the canvas, which is what an element whose parent was removed
/// does rather than collapsing to the origin.
/// </para>
/// <para>
/// The layout walks the tree from a root downwards, so a parent is resolved before its children are. A cycle — an element that is
/// its own ancestor — is not walked twice: the walk visits each element once, and an element that is reached again is left as it was.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// world.Set(panel, new RectTransformComponent { Anchored = true, SizeDelta = new Vector2(300f, 200f), Visible = true });
/// world.Set(close, new RectTransformComponent { Anchored = true, AnchorMax = Vector2.One, Pivot = Vector2.One, Visible = true });
/// world.Set(close, new ParentComponent { Parent = world.Reference(panel) });
/// </code>
/// </example>
[Component("Parent")]
public struct ParentComponent : IComponent
{
    /// <summary>Gets or sets the element that this one stands inside, or a reference that names no entity for a root.</summary>
    public EntityRef Parent;
}
