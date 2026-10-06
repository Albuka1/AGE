using Age.Core;
using Age.UI;

namespace Age.Rendering;

/// <summary>
/// Draws UI elements after the world. Single sorted pass by ZOrder. Equal ZOrder preserves ECS order.
/// </summary>
public sealed class UIRenderSystem
{
    private readonly IRenderer _renderer;
    private readonly SpriteSorter _sorter;
    private readonly List<UiItem> _items = new();

    /// <summary>Initializes the system with a renderer and a sorter.</summary>
    public UIRenderSystem(IRenderer renderer, SpriteSorter sorter)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(sorter);
        _renderer = renderer;
        _sorter = sorter;
    }

    /// <summary>Renders every visible UI element in ascending ZOrder.</summary>
    public void Render(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var screenCamera = new Camera2D
        {
            Position = Vector2.Zero,
            Zoom = 1f,
            ViewportSize = _renderer.ViewportSize,
        };

        _renderer.SetCamera(screenCamera);
        _renderer.BeginFrame(false);

        CollectItems(world);
        _sorter.Sort(_items, item => item.ZOrder);

        foreach (UiItem item in _items)
        {
            RectTransformComponent rect = world.Get<RectTransformComponent>(item.Entity);
            Rect bounds = new(rect.Position, rect.Size);

            if (item.IsButton)
            {
                _renderer.DrawRectangle(bounds, world.Get<ButtonComponent>(item.Entity).BaseColor);
            }
            else
            {
                TextLabelComponent label = world.Get<TextLabelComponent>(item.Entity);
                _renderer.DrawText(label.Text ?? string.Empty, rect.Position, label.Color);
            }
        }

        _renderer.EndFrame();
    }

    private void CollectItems(World world)
    {
        _items.Clear();

        foreach (Entity entity in world.Enumerate<RectTransformComponent>())
        {
            if (!world.Has<ButtonComponent>(entity) && !world.Has<TextLabelComponent>(entity))
            {
                continue;
            }

            RectTransformComponent rect = world.Get<RectTransformComponent>(entity);
            if (!rect.Visible)
            {
                continue;
            }

            _items.Add(new UiItem(entity, rect.ZOrder, world.Has<ButtonComponent>(entity)));
        }
    }

    private readonly record struct UiItem(Entity Entity, int ZOrder, bool IsButton);
}
