using Age.Core;
using Age.Input;

namespace Age.UI;

/// <summary>
/// Updates hover and press state for the buttons of a world.
/// </summary>
/// <remarks>
/// The system is an <see cref="IFrameSystem"/>: the pointer is a state of the frame, not of the simulation, so it runs
/// once per frame and keeps working while the clock is paused, which is what a paused game needs from its interface. A
/// click that a control of this system took is reported through <see cref="UIInputFrame"/>, which is what a system that
/// reads the same pointer after this one asks before it acts.
/// </remarks>
public sealed class UIUpdateSystem : IFrameSystem
{
    /// <summary>The depth a walk up the tree of the interface takes before it stops, which is what a cycle in a tree cannot walk past.</summary>
    private const int MaximumTreeDepth = 32;

    private readonly IInputService _input;
    private readonly UIInputFrame _frame;
    private readonly List<Entity> _buttons = new();

    /// <summary>Initializes the system with the input service it reads pointer state from.</summary>
    /// <param name="input">The input service that reports the pointer of the frame.</param>
    /// <param name="frame">The state of the frame that the controls of the interface share, or null to keep a state of its own.</param>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    public UIUpdateSystem(IInputService input, UIInputFrame? frame = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        _input = input;
        _frame = frame ?? new UIInputFrame();
    }

    /// <summary>Recomputes the hover and press state of the buttons in the given world for the current pointer position.</summary>
    /// <param name="world">The world to update.</param>
    /// <param name="frame">The time of the frame. The system does not use it.</param>
    /// <remarks>
    /// The drop-downs are read first, because an open list hangs over the elements under it and takes the pointer until it
    /// is dismissed: a click that chooses a row of a list, or one that closes it, is the list's rather than the button's
    /// that the list covered. When a drop-down takes the click, the rest of the controls are left alone for that frame.
    /// Every button first has <see cref="ButtonComponent.IsHovered"/> and <see cref="ButtonComponent.IsPressed"/> cleared,
    /// so a button that lost the pointer stops reporting a state. Then the buttons that are interactable, visible, and
    /// have a <see cref="RectTransformComponent"/> containing the pointer are considered, and the one with the highest
    /// <see cref="RectTransformComponent.ZOrder"/> wins; when two share a ZOrder the later entity wins, which matches the
    /// order the UI renderer draws them in. A press that a control of this system took is reported through
    /// <see cref="UIInputFrame.Consume"/>, which is what a system that reads the same pointer after this one asks.
    /// </remarks>
    public void UpdateFrame(World world, in GameTime frame)
    {
        ArgumentNullException.ThrowIfNull(world);

        // This system reads the pointer before the others, so it is what begins the frame for the controls of the
        // interface: what a control took on the frame before is not a click of this one.
        _frame.Begin();

        // The drop-downs read the pointer before anything else, and a click they take is not read again below: an open
        // list covers the elements under it, so a button that the list hangs over must not also be pressed by the same
        // click.
        bool consumed = UpdateDropdowns(world);

        if (consumed)
        {
            // The systems that read the same pointer after this one — a battle that would move a unit, a map that would
            // select a node — ask this rather than reading the press again, which is what keeps one click from reaching
            // two controls.
            _frame.Consume();
        }

        CollectButtons(world);

        foreach (Entity entity in _buttons)
        {
            ButtonComponent button = world.Get<ButtonComponent>(entity);
            button.IsHovered = false;
            button.IsPressed = false;
            world.Set(entity, button);
        }

        if (!consumed)
        {
            Entity? topMost = FindTopMost(world);

            if (topMost is not null)
            {
                ButtonComponent pressed = world.Get<ButtonComponent>(topMost.Value);
                pressed.IsHovered = true;
                pressed.IsPressed = _input.IsMouseButtonDown(MouseButton.Left);
                world.Set(topMost.Value, pressed);

                if (_input.IsMouseButtonPressed(MouseButton.Left))
                {
                    world.Events.Raise(new ButtonPressedEvent(topMost.Value));

                    // The button took the click of this frame, so a system that reads the same pointer below does not
                    // also read it.
                    _frame.Consume();
                }
            }
        }

        // The other controls read the same pointer. When a drop-down took the click, they are left as they are for the
        // frame, so a click that chose a row of a list does not also tick the box that stood under it.
        if (!consumed)
        {
            UpdateCheckBoxes(world);
            UpdateSliders(world);
        }
        else
        {
            // Hover is still reported, so a control the pointer is over looks live even on the frame a list took the click.
            UpdateCheckBoxes(world, pressed: false);
            UpdateSliders(world, pressed: false);
        }
    }

    /// <summary>Reads the pointer for every drop-down, opening a list, choosing a row of one that is open, and dismissing one that a click landed outside of.</summary>
    /// <param name="world">The world to read the drop-downs from.</param>
    /// <returns><see langword="true"/> when a drop-down took the click of this frame, which is what keeps it from reaching the rest of the interface.</returns>
    /// <remarks>
    /// An open list is drawn over the elements under it and takes the pointer until it is dismissed, so a click inside it
    /// chooses a row and a click outside it closes it without reaching whatever it covered. The list hangs below the
    /// closed element, so a row is found by the height of a row rather than by an element in the world.
    /// </remarks>
    private bool UpdateDropdowns(World world)
    {
        Vector2 pointer = _input.MousePosition;
        bool pressed = _input.IsMouseButtonPressed(MouseButton.Left);
        Entity? topMost = TopMost<DropdownComponent>(world, pointer, entity => world.Get<DropdownComponent>(entity).Interactable);
        Entity? open = null;

        foreach (Entity entity in world.Enumerate<DropdownComponent>())
        {
            DropdownComponent dropdown = world.Get<DropdownComponent>(entity);
            dropdown.IsHovered = topMost == entity;

            if (dropdown.Open)
            {
                open = entity;
            }

            world.Set(entity, dropdown);
        }

        if (!pressed)
        {
            return false;
        }

        // A click on the element toggles the list: closed becomes open, and open becomes closed, which is what a control
        // that is read twice by one press would otherwise do the opposite of.
        if (topMost is Entity hit)
        {
            DropdownComponent dropdown = world.Get<DropdownComponent>(hit);
            dropdown.Open = open != hit;
            world.Set(hit, dropdown);
            return true;
        }

        if (open is not Entity openEntity || !world.Has<RectTransformComponent>(openEntity))
        {
            return false;
        }

        // A click anywhere else with a list open either chooses a row of it or dismisses it, and never reaches what is
        // under the list.
        RectTransformComponent rect = world.Get<RectTransformComponent>(openEntity);
        DropdownComponent openDropdown = world.Get<DropdownComponent>(openEntity);
        float row = openDropdown.RowHeight > 0f ? openDropdown.RowHeight : rect.Size.Y;
        float listTop = rect.Position.Y + rect.Size.Y;
        float listBottom = listTop + openDropdown.ListHeight;

        openDropdown.Open = false;

        if (pointer.X >= rect.Position.X && pointer.X <= rect.Position.X + rect.Size.X && pointer.Y >= listTop && pointer.Y <= listBottom)
        {
            int index = (int)((pointer.Y - listTop) / row);

            if (index >= 0 && index < openDropdown.Count && index != openDropdown.Selected)
            {
                openDropdown.Selected = index;
                world.Events.Raise(new DropdownChangedEvent(openEntity, index, openDropdown.Value));
            }
        }

        world.Set(openEntity, openDropdown);
        return true;
    }

    private void CollectButtons(World world)
    {
        _buttons.Clear();

        foreach (Entity entity in world.Enumerate<ButtonComponent>())
        {
            _buttons.Add(entity);
        }
    }

    private Entity? FindTopMost(World world) =>
        TopMost<ButtonComponent>(world, _input.MousePosition, entity => world.Get<ButtonComponent>(entity).Interactable);

    /// <summary>Reads the pointer for every check box, toggling the state of the one that was pressed and reporting hover.</summary>
    /// <param name="world">The world to read the check boxes from.</param>
    /// <param name="pressed">Whether a press of this frame may toggle a box, which a frame that a drop-down took the click on does not.</param>
    /// <remarks>
    /// A press toggles the state and raises <see cref="CheckChangedEvent"/>, so a game reacts to a change rather than
    /// comparing the state with the frame before it. The box that the pointer is over is the one with the highest
    /// <see cref="RectTransformComponent.ZOrder"/>, so a box over a box is the one that is pressed.
    /// </remarks>
    private void UpdateCheckBoxes(World world, bool pressed = true)
    {
        Vector2 pointer = _input.MousePosition;
        Entity? topMost = TopMost<CheckBoxComponent>(world, pointer, entity => world.Get<CheckBoxComponent>(entity).Interactable);

        foreach (Entity entity in world.Enumerate<CheckBoxComponent>())
        {
            CheckBoxComponent box = world.Get<CheckBoxComponent>(entity);
            box.IsHovered = topMost == entity;
            world.Set(entity, box);
        }

        if (topMost is not Entity hit || !pressed || !_input.IsMouseButtonPressed(MouseButton.Left))
        {
            return;
        }

        CheckBoxComponent changed = world.Get<CheckBoxComponent>(hit);
        changed.Checked = !changed.Checked;
        world.Set(hit, changed);
        world.Events.Raise(new CheckChangedEvent(hit, changed.Checked));

        // The box took the click of this frame, as a button does.
        _frame.Consume();
    }

    /// <summary>Reads the pointer for every slider, beginning, following and ending a drag, and reporting the number it moved to.</summary>
    /// <param name="world">The world to read the sliders from.</param>
    /// <param name="pressed">Whether a press of this frame may begin a drag, which a frame that a drop-down took the click on does not.</param>
    /// <remarks>
    /// A drag begins on a press inside the track and follows the pointer wherever it goes until the button comes up, so a
    /// pointer that slips off the slider still moves the number, which is what a person dragging a handle expects. The
    /// number is taken from where the pointer stands along the track, so a click in the middle of the track jumps the
    /// handle there rather than nudging it. A drag that is already under way is still followed on a frame that a drop-down
    /// took a click on, so a list that opens over a track the pointer is dragging does not freeze the handle.
    /// </remarks>
    private void UpdateSliders(World world, bool pressed = true)
    {
        Vector2 pointer = _input.MousePosition;
        bool begin = pressed && _input.IsMouseButtonPressed(MouseButton.Left);
        bool held = _input.IsMouseButtonDown(MouseButton.Left);
        Entity? topMost = TopMost<SliderComponent>(world, pointer, entity => world.Get<SliderComponent>(entity).Interactable);

        foreach (Entity entity in world.Enumerate<SliderComponent>())
        {
            SliderComponent slider = world.Get<SliderComponent>(entity);
            slider.IsHovered = topMost == entity;

            // A drag that was begun ends when the button comes up, whether or not the pointer is still over the track.
            if (slider.IsDragging && !held)
            {
                slider.IsDragging = false;
            }

            if (!slider.Interactable)
            {
                slider.IsDragging = false;
                world.Set(entity, slider);
                continue;
            }

            if (!slider.IsDragging && begin && topMost == entity)
            {
                slider.IsDragging = true;

                // The drag began on this frame, so the press that began it belongs to the slider and not to a system that
                // reads the same pointer below it.
                _frame.Consume();
            }

            if (slider.IsDragging)
            {
                // The copy is written back before the number is moved, because the move reads the world through a
                // reference and the copy would otherwise be put back over it with the number it held before.
                world.Set(entity, slider);
                MoveTo(world, entity, pointer);
                world.Events.Raise(new SliderChangedEvent(entity, world.Get<SliderComponent>(entity).Value));
                continue;
            }

            world.Set(entity, slider);
        }
    }

    /// <summary>Writes the number the pointer stands at along the track of a slider.</summary>
    private static void MoveTo(World world, Entity entity, Vector2 pointer)
    {
        RectTransformComponent rect = world.Get<RectTransformComponent>(entity);
        ref SliderComponent slider = ref world.GetRef<SliderComponent>(entity);

        float width = rect.Size.X;
        slider.Normalized = width > 0f ? (pointer.X - rect.Position.X) / width : 0f;
    }

    /// <summary>Returns the topmost element of a kind that the pointer is over and that takes input, or null when it is over none.</summary>
    /// <param name="world">The world to read.</param>
    /// <param name="pointer">The position of the pointer, in the pixels of the screen.</param>
    /// <param name="interactable">Reports whether an element of the kind reacts to input, which a control that is switched off does not.</param>
    /// <returns>The element with the highest <see cref="RectTransformComponent.ZOrder"/> under the pointer, or null.</returns>
    /// <remarks>
    /// The hit test of a control is the hit test of what a person sees: an element whose root is hidden takes every
    /// element under it with it, so a button of a window that is closed is not a button the pointer is over. Without the
    /// check a window that is drawn behind another one would take the clicks of the one in front, because a hidden
    /// element is still an element of the world and the entity order of a window built later is the order that wins the
    /// tie of two ZOrders.
    /// </remarks>
    private static Entity? TopMost<T>(World world, Vector2 pointer, Func<Entity, bool> interactable)
        where T : struct, IComponent
    {
        Entity? topMost = null;
        int topZ = int.MinValue;

        foreach (Entity entity in world.Enumerate<T>())
        {
            if (!interactable(entity) || !world.Has<RectTransformComponent>(entity))
            {
                continue;
            }

            RectTransformComponent rect = world.Get<RectTransformComponent>(entity);

            if (!IsEffectivelyVisible(world, entity) || !Contains(rect, pointer))
            {
                continue;
            }

            if (topMost is null || rect.ZOrder >= topZ)
            {
                topMost = entity;
                topZ = rect.ZOrder;
            }
        }

        return topMost;
    }

    /// <summary>Returns a value indicating whether an element and every ancestor of it are visible.</summary>
    /// <param name="world">The world to read.</param>
    /// <param name="entity">The element to test.</param>
    /// <returns><see langword="true"/> when the element is drawn, which is when it and every ancestor of it say so.</returns>
    /// <remarks>
    /// The walk up the tree stops at <see cref="MaximumTreeDepth"/> levels, so a cycle a game wrote by mistake does not
    /// walk forever, and an element whose parent is gone is a root of its own: a tree that a scene dropped is a tree whose
    /// children stand where they are rather than one that is drawn as nothing.
    /// </remarks>
    private static bool IsEffectivelyVisible(World world, Entity entity)
    {
        Entity current = entity;

        for (var depth = 0; depth < MaximumTreeDepth; depth++)
        {
            if (!world.IsAlive(current) || !world.Has<RectTransformComponent>(current))
            {
                return true;
            }

            if (!world.Get<RectTransformComponent>(current).Visible)
            {
                return false;
            }

            if (!world.Has<ParentComponent>(current))
            {
                return true;
            }

            Entity parent = world.Resolve(world.Get<ParentComponent>(current).Parent);

            if (parent == default(Entity) || parent == current)
            {
                return true;
            }

            current = parent;
        }

        return true;
    }

    private static bool Contains(RectTransformComponent rect, Vector2 point) =>
        point.X >= rect.Position.X && point.X <= rect.Position.X + rect.Size.X &&
        point.Y >= rect.Position.Y && point.Y <= rect.Position.Y + rect.Size.Y;
}
