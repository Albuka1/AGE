using Age.Core;
using Age.UI;
using Microsoft.Extensions.Logging;

namespace Age.Rendering;

/// <summary>
/// Draws the visible UI elements of a world in one sorted pass, on top of the world. This type is not an
/// <see cref="ISystem"/>; a <see cref="RenderPipeline"/> runs it as a pass, after the pass of the world.
/// </summary>
/// <remarks>
/// Elements come from every entity that has a <see cref="RectTransformComponent"/> together with a
/// <see cref="ButtonComponent"/> or a <see cref="TextLabelComponent"/>. They are sorted by <see cref="RectTransformComponent.ZOrder"/>
/// with a stable sort, so elements that share a ZOrder keep their entity order. When an element carries both components,
/// its rectangle is drawn first and its label on top of it. A label that names a font is drawn with it, baked for the range
/// of characters the label covers, which is what a language whose script the built-in font does not cover needs. Add this
/// pass after the pass of the world, because the system installs a screen-space camera of its own and ignores the camera it
/// is handed: the UI does not move with the world camera.
/// </remarks>
public sealed class UIRenderSystem : IRenderPass
{
    private readonly IRenderer _renderer;
    private readonly SpriteSorter _sorter;
    private readonly IFontService? _fonts;
    private readonly ILogger<UIRenderSystem>? _logger;
    private readonly List<UiItem> _items = new();
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);

    /// <summary>Initializes the system with a renderer and a sorter.</summary>
    /// <param name="renderer">The renderer that draws the elements and owns the built-in font.</param>
    /// <param name="sorter">The sorter that puts the elements in the order of their <see cref="RectTransformComponent.ZOrder"/>.</param>
    /// <param name="fonts">The service that bakes the font a label names, or null to draw every label with the built-in font.</param>
    /// <param name="logger">The log of the game, or null to report nothing.</param>
    public UIRenderSystem(IRenderer renderer, SpriteSorter sorter, IFontService? fonts = null, ILogger<UIRenderSystem>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(sorter);
        _renderer = renderer;
        _sorter = sorter;
        _fonts = fonts;
        _logger = logger;
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
                string line = label.Text ?? string.Empty;

                if (TryFont(label, out FontHandle font))
                {
                    _fonts!.Draw(font, line, rect.Position, label.Color);
                }
                else
                {
                    _renderer.DrawText(line, rect.Position, label.Color);
                }
            }
        }

        _renderer.EndFrame();
    }

    /// <summary>Returns the font to draw a label with, baking the one that the label names.</summary>
    /// <param name="label">The label that is being drawn.</param>
    /// <param name="font">Receives the handle of the font.</param>
    /// <returns><see langword="true"/> when the label has a font of its own, <see langword="false"/> when it is drawn with the built-in one.</returns>
    /// <remarks>
    /// The service caches a font by its path, its height and its range, so a label that is drawn again asks for what was already
    /// baked. A label without a font, without a height, or with a font that cannot be baked is drawn with the built-in font,
    /// which is what a game that ships no font needs; a font that fails is reported once per path rather than every frame.
    /// </remarks>
    private bool TryFont(in TextLabelComponent label, out FontHandle font)
    {
        font = default;

        if (_fonts is null || label.FontPath is not string path || label.PixelHeight <= 0f)
        {
            return false;
        }

        (char first, char last) = Range(label);

        try
        {
            font = _fonts.Load(path, label.PixelHeight, first, last);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException)
        {
            Report(path, exception);
            return false;
        }
    }

    /// <summary>Returns the range of characters that the font of a label is baked for, which is the printable ASCII one by default.</summary>
    /// <param name="label">The label whose range is read.</param>
    /// <returns>The first and the last character of the range, with a leave of zero answered by the defaults.</returns>
    private static (char First, char Last) Range(in TextLabelComponent label) =>
        (label.FirstCharacter == '\0' ? ' ' : label.FirstCharacter, label.LastCharacter == '\0' ? '~' : label.LastCharacter);

    /// <summary>Reports a label whose font cannot be baked, once per path.</summary>
    /// <param name="path">The path of the font that was asked for.</param>
    /// <param name="exception">What stopped the bake.</param>
    /// <remarks>One line per path rather than one per frame: the same font of the same game fails in the same way however often it is drawn.</remarks>
    private void Report(string path, Exception exception)
    {
        if (_logger is not null && _reported.Add(path))
        {
            _logger.LogError("The font '{Path}' of a label of the interface cannot be baked, so the label is drawn with the built-in font: {Reason}", path, exception.Message);
        }
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
