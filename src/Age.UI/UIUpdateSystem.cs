using Age.Core;
using Age.Input;

namespace Age.UI;

/// <summary>
/// Updates hover and press state for the buttons of a world.
/// </summary>
public sealed class UIUpdateSystem : ISystem
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
    /// <param name="time">The frame time. The system does not use it.</param>
    /// <remarks>
    /// Every button first has <see cref="ButtonComponent.IsHovered"/> and <see cref="ButtonComponent.IsPressed"/> cleared,
    /// so a button that lost the pointer stops reporting a state. Then the buttons that are interactable and have a
    /// <see cref="RectTransformComponent"/> containing the pointer are considered, and the one with the highest
    /// <see cref="RectTransformComponent.ZOrder"/> wins; when two share a ZOrder the later entity wins, which matches the
    /// order the UI renderer draws them in. That button is marked hovered, and pressed while the left mouse button is held.
    /// </remarks>
    public void Update(World world, in GameTime time)
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
        if (topMost is null)
        {
            return;
        }

        ButtonComponent pressed = world.Get<ButtonComponent>(topMost.Value);
        pressed.IsHovered = true;
        pressed.IsPressed = _input.IsMouseButtonDown(MouseButton.Left);
        world.Set(topMost.Value, pressed);
    }

    private void CollectButtons(World world)
    {
        _buttons.Clear();

        foreach (Entity entity in world.Enumerate<ButtonComponent>())
        {
            _buttons.Add(entity);
        }
    }

    private Entity? FindTopMost(World world)
    {
        Vector2 pointer = _input.MousePosition;

        Entity? topMost = null;
        int topZ = int.MinValue;

        foreach (Entity entity in _buttons)
        {
            ButtonComponent button = world.Get<ButtonComponent>(entity);
            if (!button.Interactable || !world.Has<RectTransformComponent>(entity))
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
