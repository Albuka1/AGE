using Age.Core;

namespace Age.Rendering;

/// <summary>
/// A rectangle that holds other elements and draws nothing: what a row, a column or a group of elements is placed in
/// without a panel of its own.
/// </summary>
/// <remarks>
/// A group is what a window reaches for when it wants to arrange the elements of a card or a list without a background
/// behind them: the rectangle of a group is what its children are laid out against, and it is never drawn, so the frame
/// of the card is the panel of the window or of a child rather than a second one. A panel whose colour is transparent is
/// the same thing at the price of one draw call, so a game that wants the elements of a group to be drawn inside a
/// rectangle uses that instead.
/// </remarks>
public sealed class GroupHandle : UiHandle<GroupHandle>
{
    internal GroupHandle(World world, Entity entity) : base(world, entity)
    {
    }
}
