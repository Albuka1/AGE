using Age.Core;
using Age.UI;

namespace Age.Rendering;

/// <summary>
/// The shape every handle of the interface has: the entity it stands on and the moves that place it, size it and order
/// it against its siblings.
/// </summary>
/// <typeparam name="TSelf">The concrete handle, which is what the moves answer so that a chain reads as one line.</typeparam>
/// <remarks>
/// <para>
/// A handle is a wrapper over an entity and nothing else: it holds no state of a frame and no copy of a component, so
/// what it writes is what the world holds, and a game reads a value by reading the component of the entity it names.
/// The world is the one the builder was handed, which is the world a scene that replaced it is not: a handle is used
/// while the window is being built, and the entities of a window that is open belong to the world that opened them.
/// </para>
/// <para>
/// The moves place the element from the top-left corner of its parent and give it a size in pixels of the design area
/// of the canvas, which is what the <see cref="RectTransformComponent"/> reads: <see cref="At"/> anchors the element to
/// the top-left corner of its parent, <see cref="Center"/> anchors it to the middle of the parent, and
/// <see cref="AnchoredBottom"/> anchors it to the bottom of it with a margin of its own. None of them moves the element
/// inside the parent by the whole of its own size.
/// </para>
/// </remarks>
public abstract class UiHandle<TSelf> where TSelf : UiHandle<TSelf>
{
    /// <summary>The world the handle writes to, which is the one a builder was handed.</summary>
    protected readonly World World;

    /// <summary>Initializes a handle over an entity of the interface.</summary>
    /// <param name="world">The world the entity lives in.</param>
    /// <param name="entity">The entity the handle writes to.</param>
    /// <exception cref="ArgumentNullException">The world is null.</exception>
    protected UiHandle(World world, Entity entity)
    {
        ArgumentNullException.ThrowIfNull(world);
        World = world;
        Entity = entity;
    }

    /// <summary>Gets the entity the handle writes to.</summary>
    public Entity Entity { get; }

    /// <summary>Places the element from the top-left corner of its parent, in pixels of the design area.</summary>
    /// <param name="x">The distance from the left edge of the parent, in pixels.</param>
    /// <param name="y">The distance from the top edge of the parent, in pixels.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public TSelf At(float x, float y)
    {
        ref RectTransformComponent rect = ref World.GetRef<RectTransformComponent>(Entity);
        rect.Anchored = true;
        rect.AnchorMin = Vector2.Zero;
        rect.AnchorMax = Vector2.Zero;
        rect.Pivot = Vector2.Zero;
        rect.AnchoredPosition = new Vector2(x, y);
        return (TSelf)this;
    }

    /// <summary>Places the element in the middle of its parent.</summary>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public TSelf Center()
    {
        ref RectTransformComponent rect = ref World.GetRef<RectTransformComponent>(Entity);
        rect.Anchored = true;
        rect.AnchorMin = new Vector2(0.5f, 0.5f);
        rect.AnchorMax = new Vector2(0.5f, 0.5f);
        rect.Pivot = new Vector2(0.5f, 0.5f);
        rect.AnchoredPosition = Vector2.Zero;
        return (TSelf)this;
    }

    /// <summary>Stretches the element across the room of its parent, which is what a background of a panel does.</summary>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public TSelf Stretch()
    {
        ref RectTransformComponent rect = ref World.GetRef<RectTransformComponent>(Entity);
        rect.Anchored = true;
        rect.AnchorMin = Vector2.Zero;
        rect.AnchorMax = new Vector2(1f, 1f);
        rect.Pivot = new Vector2(0.5f, 0.5f);
        rect.AnchoredPosition = Vector2.Zero;
        rect.SizeDelta = Vector2.Zero;
        return (TSelf)this;
    }

    /// <summary>Anchors the element to the bottom of its parent, where a bar of the character stands.</summary>
    /// <param name="width">The width of the element, in pixels of the design area.</param>
    /// <param name="height">The height of the element, in pixels of the design area.</param>
    /// <param name="margin">The distance from the bottom edge of the parent, in pixels.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    /// <remarks>
    /// The element is placed from the left edge of its parent and its bottom stands the margin above the bottom of the
    /// parent, which is what a bar at the bottom of a window needs: the distance to the bottom holds whatever the shape
    /// of the window is, and a resolution of another size does not move the bar off the frame.
    /// </remarks>
    public TSelf AnchoredBottom(float width, float height, float margin)
    {
        ref RectTransformComponent rect = ref World.GetRef<RectTransformComponent>(Entity);
        rect.Anchored = true;
        rect.AnchorMin = new Vector2(0f, 1f);
        rect.AnchorMax = new Vector2(0f, 1f);
        rect.Pivot = new Vector2(0f, 1f);
        rect.AnchoredPosition = new Vector2(0f, -margin);
        rect.SizeDelta = new Vector2(width, height);
        return (TSelf)this;
    }

    /// <summary>Gives the element a size, in pixels of the design area of the canvas.</summary>
    /// <param name="width">The width of the element, in pixels.</param>
    /// <param name="height">The height of the element, in pixels.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public TSelf Size(float width, float height)
    {
        World.GetRef<RectTransformComponent>(Entity).SizeDelta = new Vector2(width, height);
        return (TSelf)this;
    }

    /// <summary>Orders the element against its siblings, where a larger value draws on top.</summary>
    /// <param name="z">The order of the element.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public TSelf Order(int z)
    {
        World.GetRef<RectTransformComponent>(Entity).ZOrder = z;
        return (TSelf)this;
    }

    /// <summary>Shows or hides the element, which is what a window reads back to draw or not a piece of it.</summary>
    /// <param name="visible">A value indicating whether the element is drawn.</param>
    /// <returns>The handle, so that a chain reads as one line.</returns>
    public TSelf Visible(bool visible = true)
    {
        World.GetRef<RectTransformComponent>(Entity).Visible = visible;
        return (TSelf)this;
    }
}
