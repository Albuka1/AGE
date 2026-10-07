using Age.Core;
using Age.UI;

namespace Age.Rendering;

/// <summary>
/// Draws the visible UI elements of a world in one sorted pass, on top of the world. This type is not an
/// <see cref="ISystem"/>; a <see cref="RenderPipeline"/> runs it as a pass, after the pass of the world.
/// </summary>
/// <remarks>
/// Elements come from every entity that has a <see cref="RectTransformComponent"/> together with a
/// <see cref="ButtonComponent"/> or a <see cref="TextLabelComponent"/>. They are sorted by <see cref="RectTransformComponent.ZOrder"/>
/// with a stable sort, so elements that share a ZOrder keep their entity order. When an element carries both components,
/// its rectangle is drawn first and its label on top of it. Add this pass after the pass of the world, because the
/// system installs a screen-space camera of its own and ignores the camera it is handed: the UI does not move with the
/// world camera.
/// </remarks>
public sealed class UIRenderSystem : IRenderPass
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

    /// <inheritdoc />
    public void Render(World world, in Camera2D camera)
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

            if (item.HasButton)
            {
                _renderer.DrawRectangle(new Rect(rect.Position, rect.Size), world.Get<ButtonComponent>(item.Entity).BaseColor);
            }

            if (item.HasLabel)
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

            _items.Add(new UiItem(
                entity,
                rect.ZOrder,
                world.Has<ButtonComponent>(entity),
                world.Has<TextLabelComponent>(entity)));
        }
    }

    private readonly record struct UiItem(Entity Entity, int ZOrder, bool HasButton, bool HasLabel);
}
