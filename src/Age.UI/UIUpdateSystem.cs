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

    /// <summary>
    /// Resets IsHovered and IsPressed on all buttons each frame before setting the top-most one.
    /// </summary>
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
