using Age.Core;
using Age.UI;

namespace Age.Rendering;

/// <summary>
/// Draws the lines of a world in ascending <see cref="TextComponent.ZOrder"/>. This type is not an <see cref="ISystem"/>; a
/// <see cref="RenderPipeline"/> runs it as a pass, after the pass of the world and before the pass of the UI.
/// </summary>
/// <remarks>
/// A line comes from an entity that has a <see cref="TransformComponent"/> and a <see cref="TextComponent"/> and no
/// <see cref="RectTransformComponent"/>: an entity with a rectangle is a label of the interface, which the pass of the UI
/// draws in the box of that rectangle rather than at the transform of its entity. What a line says, how it is laid out into
/// its box, and what is drawn when a font cannot be baked, is the work of <see cref="TextRenderer"/>, which this pass shares
/// with the pass of the UI, so a line of a world and a label of the interface behave the same.
/// </remarks>
/// <example>
/// <code>
/// renderPipeline.Add(renderSystem);
/// renderPipeline.Add(provider.GetRequiredService&lt;TextRenderSystem&gt;());
/// renderPipeline.Add(uiRenderSystem);
/// </code>
/// </example>
public sealed class TextRenderSystem : IRenderPass
{
    private readonly IRenderer _renderer;
    private readonly SpriteSorter _sorter;
    private readonly TextRenderer _text;
    private readonly List<Entity> _lines = new();

    /// <summary>Initializes the system with a renderer, a sorter and the renderer of text.</summary>
    /// <param name="renderer">The renderer that the frame of this pass is drawn with.</param>
    /// <param name="sorter">The sorter that puts the lines in the order of their <see cref="TextComponent.ZOrder"/>.</param>
    /// <param name="text">The renderer of text, which resolves, lays out and draws every line.</param>
    /// <exception cref="ArgumentNullException">One of the arguments is null.</exception>
    public TextRenderSystem(IRenderer renderer, SpriteSorter sorter, TextRenderer text)
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

        _renderer.SetCamera(camera);
        _renderer.BeginFrame(false);

        CollectLines(world);
        _sorter.Sort(_lines, entity => world.Get<TextComponent>(entity).ZOrder);

        foreach (Entity entity in _lines)
        {
            TextComponent line = world.Get<TextComponent>(entity);
            _text.Draw(world, entity, world.Get<TransformComponent>(entity).Position, line.Box, line);
        }

        _renderer.EndFrame();
    }

    /// <summary>Collects the lines of a world that have something to say, in ascending entity order.</summary>
    /// <param name="world">The world to read the lines from.</param>
    /// <remarks>
    /// A line needs a transform, for where it starts, and something to say; a text that says nothing is left out here, so a
    /// game may keep a line in a world and let it say nothing. A text that also has a rectangle belongs to the interface,
    /// which the pass of the UI draws.
    /// </remarks>
    private void CollectLines(World world)
    {
        _lines.Clear();

        foreach (Entity entity in world.Enumerate<TextComponent>())
        {
            if (!world.Has<TransformComponent>(entity) || world.Has<RectTransformComponent>(entity))
            {
                continue;
            }

            TextComponent text = world.Get<TextComponent>(entity);
            if (string.IsNullOrEmpty(text.Text) && string.IsNullOrEmpty(text.Key))
            {
                continue;
            }

            _lines.Add(entity);
        }
    }
}
