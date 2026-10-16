using Age.Core;
using Age.UI;

namespace Age.Rendering;

/// <summary>
/// Draws the visible UI elements of a world in one sorted pass, on top of the world. This type is not an
/// <see cref="ISystem"/>; a <see cref="RenderPipeline"/> runs it as a pass, after the pass of the world.
/// </summary>
/// <remarks>
/// Elements come from every entity that has a <see cref="RectTransformComponent"/> together with a component of the
/// interface. They are sorted by <see cref="RectTransformComponent.ZOrder"/> with a stable sort, so elements that share
/// a ZOrder keep their entity order. An element is drawn only when it and every ancestor of it are visible, which is what
/// a window hides a whole tree with. Add this pass after the pass of the world, because the system installs a
/// screen-space camera of its own and ignores the camera it is handed: the UI does not move with the world camera.
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

    /// <summary>The depth a walk up the tree of the interface takes before it stops, which is what a cycle in a tree cannot walk past.</summary>
    private const int MaximumTreeDepth = 32;

    private readonly IRenderer _renderer;
    private readonly SpriteSorter _sorter;
    private readonly TextRenderer _text;
    private readonly ITextureService? _textures;
    private readonly List<UiItem> _items = new();

    /// <summary>Initializes the system with a renderer, a sorter and the renderer of text.</summary>
    /// <param name="renderer">The renderer that draws the elements.</param>
    /// <param name="sorter">The sorter that puts the elements in the order of their <see cref="RectTransformComponent.ZOrder"/>.</param>
    /// <param name="text">The renderer of text, which resolves, lays out and draws every label of the interface.</param>
    /// <param name="textures">The service that resolves the image of an element, or null to draw none of them.</param>
    /// <exception cref="ArgumentNullException">One of the arguments that is not optional is null.</exception>
    public UIRenderSystem(IRenderer renderer, SpriteSorter sorter, TextRenderer text, ITextureService? textures = null)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(sorter);
        ArgumentNullException.ThrowIfNull(text);

        _renderer = renderer;
        _sorter = sorter;
        _text = text;
        _textures = textures;
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

            if (item.HasPanel)
            {
                PanelComponent panel = world.Get<PanelComponent>(item.Entity);

                _renderer.DrawRectangle(box, panel.Color);
                DrawBorder(box, panel.BorderColor, panel.BorderThickness);
            }

            if (item.HasImage && _textures is not null)
            {
                DrawImage(world.Get<ImageComponent>(item.Entity), box);
            }

            if (item.HasProgressBar)
            {
                Color fill = item.HasPanel ? world.Get<PanelComponent>(item.Entity).Color : FaceColour;
                DrawProgressBar(world.Get<ProgressBarComponent>(item.Entity), box, fill);
            }

            if (item.HasButton)
            {
                ButtonComponent button = world.Get<ButtonComponent>(item.Entity);

                _renderer.DrawRectangle(box, button.BaseColor);
                DrawBorder(box, button.BorderColor, button.BorderThickness);
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

            if (item.HasTextField)
            {
                DrawTextField(world.Get<TextFieldComponent>(item.Entity), box);
            }
        }

        foreach (UiItem item in _items)
        {
            if (!item.HasDropdown)
            {
                continue;
            }

            DropdownComponent dropdown = world.Get<DropdownComponent>(item.Entity);

            if (!dropdown.Open)
            {
                continue;
            }

            RectTransformComponent rect = world.Get<RectTransformComponent>(item.Entity);
            DrawDropdownList(dropdown, new Rect(rect.Position, rect.Size));
        }

        _renderer.EndFrame();
    }

    /// <summary>Draws the frame of a rectangle, which is what a panel and a button say about their edge.</summary>
    /// <param name="box">The rectangle the frame is drawn inside.</param>
    /// <param name="color">The colour of the frame, where a transparent colour draws nothing.</param>
    /// <param name="thickness">How many pixels thick the frame is, where zero draws nothing.</param>
    /// <remarks>
    /// The frame is four rectangles rather than a rounded rectangle or a picture, because the rectangle is what the
    /// renderer draws and the interface of the engine is drawn from rectangles alone. It is drawn inside the box of the
    /// element rather than beside it, so an element that grows a frame keeps the room it was given.
    /// </remarks>
    private void DrawBorder(Rect box, Color color, int thickness)
    {
        if (thickness <= 0 || color.A == 0 || box.Width <= 0f || box.Height <= 0f)
        {
            return;
        }

        int edge = Math.Min(thickness, (int)MathF.Min(box.Width, box.Height));

        if (edge <= 0)
        {
            return;
        }

        _renderer.DrawRectangle(new Rect(box.Position, new Vector2(box.Width, edge)), color);
        _renderer.DrawRectangle(new Rect(box.Position + new Vector2(0f, box.Height - edge), new Vector2(box.Width, edge)), color);
        _renderer.DrawRectangle(new Rect(box.Position, new Vector2(edge, box.Height)), color);
        _renderer.DrawRectangle(new Rect(box.Position + new Vector2(box.Width - edge, 0f), new Vector2(edge, box.Height)), color);
    }

    /// <summary>Draws the image of an element inside its rectangle, or its placeholder when the image is not there.</summary>
    private void DrawImage(in ImageComponent image, Rect box)
    {
        if (_textures is null || string.IsNullOrEmpty(image.TexturePath))
        {
            return;
        }

        TextureHandle texture = _textures.Resolve(image.TexturePath);
        Color tint = image.Tint.A == 0 ? Color.White : image.Tint;

        _renderer.DrawSprite(texture, box.Position, box.Size, tint);
    }

    /// <summary>Draws a check box: a face, and a mark in it when it is checked.</summary>
    private void DrawCheckBox(in CheckBoxComponent checkBox, Rect box)
    {
        _renderer.DrawRectangle(box, checkBox.IsHovered ? HoverColour : FaceColour);

        if (!checkBox.Checked)
        {
            return;
        }

        float inset = MathF.Max(3f, MathF.Min(box.Width, box.Height) * 0.25f);
        _renderer.DrawRectangle(new Rect(box.Position + new Vector2(inset, inset), box.Size - new Vector2(inset * 2f, inset * 2f)), MarkColour);
    }

    /// <summary>Draws a slider: a track, the part of it the value has reached, and a handle at the value.</summary>
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
    private void DrawProgressBar(in ProgressBarComponent bar, Rect box, Color fill)
    {
        _renderer.DrawRectangle(box, InkColour);
        _renderer.DrawRectangle(new Rect(box.Position, new Vector2(box.Width * bar.Normalized, box.Height)), fill);
    }

    /// <summary>Draws the closed element of a drop-down, which is the chosen value on a face.</summary>
    private void DrawDropdown(in DropdownComponent dropdown, Rect box)
    {
        _renderer.DrawRectangle(box, dropdown.IsHovered ? HoverColour : FaceColour);

        if (dropdown.Count == 0)
        {
            return;
        }

        _renderer.DrawText(dropdown.Value, box.Position + new Vector2(LinePad, 0f), TextColour);
    }

    /// <summary>Draws the open list of a drop-down, which hangs under the closed element and stands over the rest of the interface.</summary>
    private void DrawDropdownList(in DropdownComponent dropdown, Rect box)
    {
        if (dropdown.Count == 0)
        {
            return;
        }

        float row = dropdown.RowHeight > 0f ? dropdown.RowHeight : box.Height;

        for (var index = 0; index < dropdown.Count; index++)
        {
            var rowBox = new Rect(new Vector2(box.Position.X, box.Position.Y + box.Height + (index * row)), new Vector2(box.Width, row));

            _renderer.DrawRectangle(rowBox, index == dropdown.Selected ? MarkColour : ListColour);
            _renderer.DrawText(dropdown.Options![index], rowBox.Position + new Vector2(LinePad, 0f), TextColour);
        }
    }

    /// <summary>Draws the line of a text field and the caret at its place.</summary>
    private void DrawTextField(in TextFieldComponent field, Rect box)
    {
        _renderer.DrawRectangle(box, field.IsHovered || field.Focused ? HoverColour : FaceColour);
        _renderer.DrawText(field.Value, box.Position + new Vector2(LinePad, 0f), TextColour);

        if (!field.Focused)
        {
            return;
        }

        float advance = field.Caret * BitmapFontMetrics.GlyphWidth;
        var caret = new Rect(
            new Vector2(box.Position.X + LinePad + advance, box.Position.Y + 2f),
            new Vector2(Border, MathF.Max(1f, box.Height - 4f)));

        _renderer.DrawRectangle(caret, MarkColour);
    }

    /// <summary>Collects the elements of a world that are visible, in ascending entity order.</summary>
    /// <remarks>
    /// An element is what a person sees when it is visible and every ancestor of it is visible: a panel that a window
    /// hides takes its children with it, which is what a window of the interface is for. The walk up the tree stops at
    /// <see cref="MaximumTreeDepth"/> levels, so a cycle a game wrote by mistake does not walk forever.
    /// </remarks>
    private void CollectItems(World world)
    {
        _items.Clear();

        foreach (Entity entity in world.Enumerate<RectTransformComponent>())
        {
            if (!IsVisibleElement(world, entity) || !IsEffectivelyVisible(world, entity))
            {
                continue;
            }

            RectTransformComponent rect = world.Get<RectTransformComponent>(entity);

            _items.Add(new UiItem(
                entity,
                rect.ZOrder,
                world.Has<PanelComponent>(entity),
                world.Has<ImageComponent>(entity),
                world.Has<ButtonComponent>(entity),
                world.Has<TextComponent>(entity),
                world.Has<CheckBoxComponent>(entity),
                world.Has<SliderComponent>(entity),
                world.Has<ProgressBarComponent>(entity),
                world.Has<DropdownComponent>(entity),
                world.Has<TextFieldComponent>(entity)));
        }
    }

    /// <summary>Returns a value indicating whether an element and every ancestor of it are visible.</summary>
    private static bool IsEffectivelyVisible(World world, Entity entity)
    {
        Entity current = entity;

        for (var depth = 0; depth < MaximumTreeDepth; depth++)
        {
            if (!world.IsAlive(current) || !world.Has<RectTransformComponent>(current))
            {
                // An element of a parent that is gone is a root of its own, and one that a game holds without a
                // rectangle is not one this pass reads: the two are left to whatever placed them.
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

    /// <summary>Returns a value indicating whether an element carries something this pass draws.</summary>
    private static bool IsVisibleElement(World world, Entity entity) =>
        world.Has<PanelComponent>(entity) ||
        world.Has<ImageComponent>(entity) ||
        world.Has<ButtonComponent>(entity) ||
        world.Has<TextComponent>(entity) ||
        world.Has<CheckBoxComponent>(entity) ||
        world.Has<SliderComponent>(entity) ||
        world.Has<ProgressBarComponent>(entity) ||
        world.Has<DropdownComponent>(entity) ||
        world.Has<TextFieldComponent>(entity);

    private readonly record struct UiItem(
        Entity Entity,
        int ZOrder,
        bool HasPanel,
        bool HasImage,
        bool HasButton,
        bool HasLabel,
        bool HasCheckBox,
        bool HasSlider,
        bool HasProgressBar,
        bool HasDropdown,
        bool HasTextField);
}
