using Age.Core;
using Age.Input;

namespace Age.UI;

/// <summary>
/// Updates hover and press state for the buttons of a world.
/// </summary>
/// <remarks>
/// The system is an <see cref="IFrameSystem"/>: the pointer is a state of the frame, not of the simulation, so it runs
/// once per frame and keeps working while the clock is paused, which is what a paused game needs from its interface.
/// </remarks>
public sealed class UIUpdateSystem : IFrameSystem
{
    private readonly IInputService _input;
    private readonly List<Entity> _buttons = new();

    /// <summary>Initializes the system with the input service it reads pointer state from.</summary>
    public UIUpdateSystem(IInputService input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _input = input;
    }

    /// <summary>Recomputes the hover and press state of the buttons in the given world for the current pointer position.</summary>
    /// <param name="world">The world to update.</param>
    /// <param name="frame">The time of the frame. The system does not use it.</param>
    /// <remarks>
    /// Every button first has <see cref="ButtonComponent.IsHovered"/> and <see cref="ButtonComponent.IsPressed"/> cleared,
    /// so a button that lost the pointer stops reporting a state. Then the buttons that are interactable and have a
    /// <see cref="RectTransformComponent"/> containing the pointer are considered, and the one with the highest
    /// <see cref="RectTransformComponent.ZOrder"/> wins; when two share a ZOrder the later entity wins, which matches the
    /// order the UI renderer draws them in. That button is marked hovered, and pressed while the left mouse button is held.
    /// </remarks>
    public void UpdateFrame(World world, in GameTime frame)
    {
        ArgumentNullException.ThrowIfNull(world);

        CollectButtons(world);

        foreach (Entity entity in _buttons)
        {
            ButtonComponent button = world.Get<ButtonComponent>(entity);
            button.IsHovered = false;
            button.IsPressed = false;
            world.Set(entity, button);
        }

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
            }
        }

        // The other controls read the same pointer. A click that lands on a slider is the slider's, so the drag begins before a check
        // box that stands under the pointer claims it, and a control that stands over another wins by its ZOrder rather than the order
        // the two are read in.
        UpdateCheckBoxes(world);
        UpdateSliders(world);
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
    /// <remarks>
    /// A press toggles the state and raises <see cref="CheckChangedEvent"/>, so a game reacts to a change rather than comparing the
    /// state with the frame before it. The box that the pointer is over is the one with the highest
    /// <see cref="RectTransformComponent.ZOrder"/>, so a box over a box is the one that is pressed.
    /// </remarks>
    private void UpdateCheckBoxes(World world)
    {
        Vector2 pointer = _input.MousePosition;
        Entity? topMost = TopMost<CheckBoxComponent>(world, pointer, entity => world.Get<CheckBoxComponent>(entity).Interactable);

        foreach (Entity entity in world.Enumerate<CheckBoxComponent>())
        {
            CheckBoxComponent box = world.Get<CheckBoxComponent>(entity);
            box.IsHovered = topMost == entity;
            world.Set(entity, box);
        }

        if (topMost is not Entity hit || !_input.IsMouseButtonPressed(MouseButton.Left))
        {
            return;
        }

        CheckBoxComponent changed = world.Get<CheckBoxComponent>(hit);
        changed.Checked = !changed.Checked;
        world.Set(hit, changed);
        world.Events.Raise(new CheckChangedEvent(hit, changed.Checked));
    }

    /// <summary>Reads the pointer for every slider, beginning, following and ending a drag, and reporting the number it moved to.</summary>
    /// <remarks>
    /// A drag begins on a press inside the track and follows the pointer wherever it goes until the button comes up, so a pointer that
    /// slips off the slider still moves the number, which is what a person dragging a handle expects. The number is taken from where
    /// the pointer stands along the track, so a click in the middle of the track jumps the handle there rather than nudging it.
    /// </remarks>
    private void UpdateSliders(World world)
    {
        Vector2 pointer = _input.MousePosition;
        bool pressed = _input.IsMouseButtonPressed(MouseButton.Left);
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

            if (!slider.IsDragging && pressed && topMost == entity)
            {
                slider.IsDragging = true;
            }

            if (slider.IsDragging)
            {
                // The copy is written back before the number is moved, because the move reads the world through a reference and the
                // copy would otherwise be put back over it with the number it held before.
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

            if (!Contains(rect, pointer))
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

    private static bool Contains(RectTransformComponent rect, Vector2 point) =>
        point.X >= rect.Position.X && point.X <= rect.Position.X + rect.Size.X &&
        point.Y >= rect.Position.Y && point.Y <= rect.Position.Y + rect.Size.Y;
}
