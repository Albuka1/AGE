using Age.Core;
using Microsoft.Extensions.Logging;

namespace Age.Rendering;

/// <summary>
/// Draws the text of a world in ascending <see cref="TextComponent.ZOrder"/>. This type is not an <see cref="ISystem"/>; a
/// <see cref="RenderPipeline"/> runs it as a pass, after the pass of the world and before the pass of the UI.
/// </summary>
/// <remarks>
/// <para>
/// A line needs a transform, for where it starts, and a <see cref="TextComponent"/>, for what it says. A line whose text is
/// empty or whose height is zero or less is not drawn, so a game may keep a line in a world and let it say nothing.
/// </para>
/// <para>
/// The font of a line is baked for the range its component names and is then cached by the service, so a line is baked once
/// and every frame after the first draws what it resolved. A line whose font cannot be baked is drawn with the built-in font
/// of the renderer, which is the font that the UI pass draws with, and is reported once per path: a mistake in the content of
/// a game is visible and cheap rather than fatal.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var text = new TextRenderSystem(renderer, sorter, fonts);
/// renderPipeline.Add(renderSystem);
/// renderPipeline.Add(text);
/// renderPipeline.Add(uiRenderSystem);
/// </code>
/// </example>
public sealed class TextRenderSystem : IRenderPass
{
    private readonly IRenderer _renderer;
    private readonly SpriteSorter _sorter;
    private readonly IFontService? _fonts;
    private readonly ILogger<TextRenderSystem>? _logger;
    private readonly List<Entity> _lines = new();
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);

    /// <summary>Initializes the system with a renderer and a sorter.</summary>
    /// <param name="renderer">The renderer that draws the lines and owns the built-in font.</param>
    /// <param name="sorter">The sorter that puts the lines in the order of their <see cref="TextComponent.ZOrder"/>.</param>
    /// <param name="fonts">The service that bakes the font a line names, or null to draw every line with the built-in font.</param>
    /// <param name="logger">The log of the game, or null to report nothing.</param>
    /// <remarks>A container passes the font service and the log, which is what lets a line of content name its font.</remarks>
    public TextRenderSystem(IRenderer renderer, SpriteSorter sorter, IFontService? fonts = null, ILogger<TextRenderSystem>? logger = null)
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

        _renderer.SetCamera(camera);
        _renderer.BeginFrame(false);

        CollectLines(world);
        _sorter.Sort(_lines, entity => world.Get<TextComponent>(entity).ZOrder);

        foreach (Entity entity in _lines)
        {
            TextComponent text = world.Get<TextComponent>(entity);
            Vector2 position = world.Get<TransformComponent>(entity).Position;
            string line = text.Text ?? string.Empty;

            if (!TryFont(world, entity, text, out FontHandle font))
            {
                _renderer.DrawText(line, position, text.Color);
                continue;
            }

            _fonts!.Draw(font, line, position, text.Color);
        }

        _renderer.EndFrame();
    }

    /// <summary>Returns the font to draw a line with, baking the one that the line names.</summary>
    /// <param name="world">The world that holds the line, which the handle of the baked font is written back into.</param>
    /// <param name="entity">The entity that carries the line.</param>
    /// <param name="text">The line that is being drawn.</param>
    /// <param name="font">Receives the handle of the font.</param>
    /// <returns><see langword="true"/> when the line has a font of its own, <see langword="false"/> when it is drawn with the built-in one.</returns>
    /// <remarks>
    /// The service caches a font by its path, its height and its range, so a line that is drawn again asks for what was already
    /// baked and the frame after the first one draws what the last one resolved. A font that cannot be read, that is not there,
    /// or that needs more pixels than an atlas of a font holds is reported once and answered with the built-in font, which keeps
    /// a line readable instead of taking the frame down.
    /// </remarks>
    private bool TryFont(World world, Entity entity, in TextComponent text, out FontHandle font)
    {
        font = text.Font;

        if (_fonts is null || text.FontPath is not string path)
        {
            return false;
        }

        (char first, char last) = Range(text);

        try
        {
            font = _fonts.Load(path, text.PixelHeight, first, last);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException)
        {
            Report(path, exception);
            return false;
        }

        // The handle is written back only when it changed, so a line that is drawn again with the font it already resolved
        // leaves the world alone.
        if (font != text.Font)
        {
            world.GetRef<TextComponent>(entity).Font = font;
        }

        return true;
    }

    /// <summary>Returns the range of characters that the font of a line is baked for, which is the printable ASCII one by default.</summary>
    /// <param name="text">The line whose range is read.</param>
    /// <returns>The first and the last character of the range, with a leave of zero answered by the defaults.</returns>
    private static (char First, char Last) Range(in TextComponent text) =>
        (text.FirstCharacter == '\0' ? ' ' : text.FirstCharacter, text.LastCharacter == '\0' ? '~' : text.LastCharacter);

    /// <summary>Reports a line whose font cannot be baked, once per path.</summary>
    /// <param name="path">The path of the font that was asked for.</param>
    /// <param name="exception">What stopped the bake.</param>
    /// <remarks>One line per path rather than one per frame: the same font of the same game fails in the same way however often it is drawn.</remarks>
    private void Report(string path, Exception exception)
    {
        if (_logger is not null && _reported.Add(path))
        {
            _logger.LogError("The font '{Path}' of a line of text cannot be baked, so the line is drawn with the built-in font: {Reason}", path, exception.Message);
        }
    }

    /// <summary>Collects the lines of the world that have something to say, in ascending entity order.</summary>
    /// <param name="world">The world to read the lines from.</param>
    /// <remarks>A line without a transform has no place to start at, and one without text or without a height has nothing to say.</remarks>
    private void CollectLines(World world)
    {
        _lines.Clear();

        foreach (Entity entity in world.Enumerate<TextComponent>())
        {
            if (!world.Has<TransformComponent>(entity))
            {
                continue;
            }

            TextComponent text = world.Get<TextComponent>(entity);

            if (string.IsNullOrEmpty(text.Text) || text.PixelHeight <= 0f)
            {
                continue;
            }

            _lines.Add(entity);
        }
    }
}
