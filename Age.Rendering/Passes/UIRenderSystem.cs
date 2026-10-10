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
    private static readonly Color FaceColour = new(44, 48, 60);
    private static readonly Color HoverColour = new(62, 68, 84);
    private static readonly Color InkColour = new(22, 24, 30);
    private static readonly Color MarkColour = new(120, 170, 240);
    private static readonly Color HandleColour = new(220, 224, 232);
    private static readonly Color ListColour = new(30, 33, 40);
    private static readonly Color TextColour = new(225, 225, 225);

    private const float TrackHeight = 8f;
    private const float HandleWidth = 10f;
    private const float Border = 2f;
    private const float LinePad = 6f;

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
            var box = new Rect(rect.Position, rect.Size);

            if (item.HasButton)
            {
                _renderer.DrawRectangle(box, world.Get<ButtonComponent>(item.Entity).BaseColor);
            }

            if (item.HasProgressBar)
            {
                DrawProgressBar(world.Get<ProgressBarComponent>(item.Entity), box);
            }

            if (item.HasSlider)
            {
                DrawSlider(world.Get<SliderComponent>(item.Entity), box);
            }

            if (item.HasCheckBox)
            {
                DrawCheckBox(world.Get<CheckBoxComponent>(item.Entity), box);
            }

            if (item.HasDropdown)
            {
                DrawDropdown(world.Get<DropdownComponent>(item.Entity), box);
            }

            if (item.HasLabel)
            {
                TextComponent label = world.Get<TextComponent>(item.Entity);
                Vector2 textBox = label.Box == Vector2.Zero ? rect.Size : label.Box;

                _text.Draw(world, item.Entity, rect.Position, textBox, label);
            }

            // The line of a text field is not a text of the world, so it is drawn as a line of the interface rather than through a
            // component: the field holds the string and the caret, and the pass draws them where the element stands.
            if (item.HasTextField)
            {
                DrawTextField(world.Get<TextFieldComponent>(item.Entity), box);
            }
        }

        _renderer.EndFrame();
    }

    /// <summary>Draws a check box: a face, and a mark in it when it is checked.</summary>
    /// <param name="checkBox">The state of the box.</param>
    /// <param name="box">The rectangle of the element.</param>
    private void DrawCheckBox(in CheckBoxComponent checkBox, Rect box)
    {
        _renderer.DrawRectangle(box, checkBox.IsHovered ? HoverColour : FaceColour);

        if (!checkBox.Checked)
        {
            return;
        }

        // The mark is a rectangle inset from the box rather than a glyph, so a check box is drawn without a font.
        float inset = MathF.Max(3f, MathF.Min(box.Width, box.Height) * 0.25f);
        _renderer.DrawRectangle(new Rect(box.Position + new Vector2(inset, inset), box.Size - new Vector2(inset * 2f, inset * 2f)), MarkColour);
    }

    /// <summary>Draws a slider: a track, the part of it the value has reached, and a handle at the value.</summary>
    /// <param name="slider">The value of the slider.</param>
    /// <param name="box">The rectangle of the element.</param>
    private void DrawSlider(in SliderComponent slider, Rect box)
    {
        float height = MathF.Min(box.Height, TrackHeight);
        float top = box.Position.Y + ((box.Height - height) / 2f);
        var track = new Rect(new Vector2(box.Position.X, top), new Vector2(box.Width, height));

        _renderer.DrawRectangle(track, InkColour);
        _renderer.DrawRectangle(new Rect(track.Position, new Vector2(track.Width * slider.Normalized, track.Height)), slider.IsHovered || slider.IsDragging ? MarkColour : FaceColour);

        float handle = MathF.Min(HandleWidth, MathF.Max(4f, box.Width / 4f));
        float x = box.Position.X + (box.Width * slider.Normalized);

        _renderer.DrawRectangle(new Rect(new Vector2(x - (handle / 2f), box.Position.Y), new Vector2(handle, box.Height)), HandleColour);
    }

    /// <summary>Draws a progress bar: a track and the part of it the value has reached.</summary>
    /// <param name="bar">The value of the bar.</param>
    /// <param name="box">The rectangle of the element.</param>
    private void DrawProgressBar(in ProgressBarComponent bar, Rect box)
    {
        _renderer.DrawRectangle(box, InkColour);
        _renderer.DrawRectangle(new Rect(box.Position, new Vector2(box.Width * bar.Normalized, box.Height)), FaceColour);
    }

    /// <summary>Draws a drop-down: the chosen value in the closed element, and the whole list under it when it is open.</summary>
    /// <param name="dropdown">The values of the drop-down.</param>
    /// <param name="box">The rectangle of the closed element.</param>
    private void DrawDropdown(in DropdownComponent dropdown, Rect box)
    {
        _renderer.DrawRectangle(box, dropdown.IsHovered ? HoverColour : FaceColour);

        if (dropdown.Count == 0)
        {
            return;
        }

        float row = dropdown.RowHeight > 0f ? dropdown.RowHeight : box.Height;
        _renderer.DrawText(dropdown.Value, box.Position + new Vector2(LinePad, 0f), TextColour);

        if (!dropdown.Open)
        {
            return;
        }

        for (var index = 0; index < dropdown.Count; index++)
        {
            var rowBox = new Rect(new Vector2(box.Position.X, box.Position.Y + box.Height + (index * row)), new Vector2(box.Width, row));

            _renderer.DrawRectangle(rowBox, index == dropdown.Selected ? MarkColour : ListColour);
            _renderer.DrawText(dropdown.Options![index], rowBox.Position + new Vector2(LinePad, 0f), TextColour);
        }
    }

    /// <summary>Draws the line of a text field and the caret at its place.</summary>
    /// <param name="field">The line the field holds.</param>
    /// <param name="box">The rectangle of the element.</param>
    private void DrawTextField(in TextFieldComponent field, Rect box)
    {
        _renderer.DrawRectangle(box, field.IsHovered || field.Focused ? HoverColour : FaceColour);
        _renderer.DrawText(field.Value, box.Position + new Vector2(LinePad, 0f), TextColour);

        if (!field.Focused)
        {
            return;
        }

        // The caret stands where the characters before it end, which the built-in font measures at a fixed width per glyph.
        float advance = field.Caret * BitmapFontMetrics.GlyphWidth;
        var caret = new Rect(
            new Vector2(box.Position.X + LinePad + advance, box.Position.Y + 2f),
            new Vector2(Border, MathF.Max(1f, box.Height - 4f)));

        _renderer.DrawRectangle(caret, MarkColour);
    }

    /// <summary>Collects the elements of a world that are visible, in ascending entity order.</summary>
    /// <param name="world">The world to read the elements from.</param>
    private void CollectItems(World world)
    {
        _items.Clear();

        foreach (Entity entity in world.Enumerate<RectTransformComponent>())
        {
            if (!IsVisibleElement(world, entity))
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
                world.Has<TextComponent>(entity),
                world.Has<CheckBoxComponent>(entity),
                world.Has<SliderComponent>(entity),
                world.Has<ProgressBarComponent>(entity),
                world.Has<DropdownComponent>(entity),
                world.Has<TextFieldComponent>(entity)));
        }
    }

    /// <summary>Returns a value indicating whether an element carries something this pass draws.</summary>
    private static bool IsVisibleElement(World world, Entity entity) =>
        world.Has<ButtonComponent>(entity) || world.Has<TextComponent>(entity) || world.Has<CheckBoxComponent>(entity) ||
        world.Has<SliderComponent>(entity) || world.Has<ProgressBarComponent>(entity) || world.Has<DropdownComponent>(entity) ||
        world.Has<TextFieldComponent>(entity);

    private readonly record struct UiItem(
        Entity Entity,
        int ZOrder,
        bool HasButton,
        bool HasLabel,
        bool HasCheckBox,
        bool HasSlider,
        bool HasProgressBar,
        bool HasDropdown,
        bool HasTextField);
}
