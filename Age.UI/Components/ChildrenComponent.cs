using Age.Core;

namespace Age.UI;

/// <summary>
/// Keeps the elements that stand inside this one, in the order they were added, which is what makes the tree walkable downwards.
/// </summary>
/// <remarks>
/// <para>
/// A parent keeps this list and a child keeps its <see cref="ParentComponent"/>, so the tree can be walked from either end: the layout
/// follows the list downwards, and a child that wants to know where it stands reads its own component. A game writes the two together
/// — a helper that sets both is better than a list that disagrees with the components of its children, which is a tree with an element
/// in two places or in none.
/// </para>
/// <para>
/// The references are <see cref="EntityRef"/>s, so a tree survives a save and a load exactly as a single parent link does. An element
/// that a world no longer holds is skipped rather than failing the walk, which is what removing a child without re-parenting it leaves
/// behind.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// world.Set(panel, new ChildrenComponent { Children = [world.Reference(heading), world.Reference(close)] });
/// </code>
/// </example>
[Component("Children")]
public struct ChildrenComponent : IComponent
{
    /// <summary>Gets or sets the elements that stand inside this one, in the order they are drawn and walked, or null for none.</summary>
    public EntityRef[]? Children;
}
