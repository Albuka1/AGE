using Age.Core;
using Age.UI;

namespace Age.Rendering;

/// <summary>
/// Draws the visible UI elements of a world in one sorted pass, on top of the world. This type is not an
/// <see cref="ISystem"/>; a <see cref="RenderPipeline"/> runs it as a pass, after the pass of the world.
/// </summary>
/// <remarks>
/// Elements come from every entity that has a <see cref="RectTransformComponent"/> together with a
/// <see cref="ButtonComponent"/> or a <see cref="TextComponent"/>. They are sorted by
/// <see cref="RectTransformComponent.ZOrder"/> with a stable sort, so elements that share a ZOrder keep their entity order.
/// When an element carries a button and a text, its rectangle is drawn first and its text on top of it, laid out into the
/// box of the rectangle, which is the size the element has and not the size the text needs: the alignment of the style is
/// what centers a label in a button. Add this pass after the pass of the world, because the system installs a screen-space
/// camera of its own and ignores the camera it is handed: the UI does not move with the world camera.
/// </remarks>
public sealed class UIRenderSystem : IRenderPass
{
    private readonly IRenderer _renderer;
    private readonly SpriteSorter _sorter;
    private readonly TextRenderer _text;
    private readonly List<UiItem> _items = new();

    /// <summary>Initializes the system with a renderer, a sorter and the renderer of text.</summary>
    /// <param name="renderer">The renderer that draws the elements.</param>
    /// <param name="sorter">The sorter that puts the elements in the order of their <see cref="RectTransformComponent.ZOrder"/>.</param>
    /// <param name="text">The renderer of text, which resolves, lays out and draws every label of the interface.</param>
    /// <exception cref="ArgumentNullException">One of the arguments is null.</exception>
    public UIRenderSystem(IRenderer renderer, SpriteSorter sorter, TextRenderer text)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(sorter);
        ArgumentNullException.ThrowIfNull(text);
        _renderer = renderer;
        _sorter = sorter;
        _text = text;
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
                TextComponent label = world.Get<TextComponent>(item.Entity);
                Vector2 box = label.Box == Vector2.Zero ? rect.Size : label.Box;

                _text.Draw(world, item.Entity, rect.Position, box, label);
            }
        }

        _renderer.EndFrame();
    }

    /// <summary>Collects the elements of a world that are visible, in ascending entity order.</summary>
    /// <param name="world">The world to read the elements from.</param>
    private void CollectItems(World world)
    {
        _items.Clear();

        foreach (Entity entity in world.Enumerate<RectTransformComponent>())
        {
            if (!world.Has<ButtonComponent>(entity) && !world.Has<TextComponent>(entity))
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
                world.Has<TextComponent>(entity)));
        }
    }

    private readonly record struct UiItem(Entity Entity, int ZOrder, bool HasButton, bool HasLabel);
}
