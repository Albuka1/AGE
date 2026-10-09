using Age.Core;
using Age.UI;

namespace Age.Rendering;

/// <summary>
/// Resolves the position and the size of every anchored element of the interface from the canvas that its world holds and the
/// scale of it. This type is not an <see cref="IRenderPass"/>: it is a frame system, because what the interface is laid out
/// against is the size of the window, which is a state of the frame rather than of the simulation.
/// </summary>
/// <remarks>
/// <para>
/// Add it to the pipeline with <c>AddFrame</c> before the frame systems and the passes that read the rectangle of an element:
/// <see cref="UIUpdateSystem"/> compares the pointer with it and <see cref="UIRenderSystem"/> draws it, so a layout that runs
/// after either of them is a frame behind. It runs while the clock is paused, which is what a menu needs.
/// </para>
/// <para>
/// A world without a canvas is laid out against the window itself at a scale of one, which is all an element that is anchored
/// to a corner of the screen needs; a world with one is laid out against the canvas that is marked as the root, or against the
/// first canvas of the world when none of them is marked. A renderer that is not attached to a window has no screen to lay
/// anything out on, so what a game authored stays as it is rather than collapsing to the origin of it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// pipeline.AddFrame(provider.GetRequiredService&lt;UILayoutSystem&gt;());
/// pipeline.AddFrame(provider.GetRequiredService&lt;UIUpdateSystem&gt;());
/// </code>
/// </example>
public sealed class UILayoutSystem : IFrameSystem
{
    private readonly IRenderer _renderer;

    /// <summary>Initializes the system with the renderer whose viewport is the screen.</summary>
    /// <param name="renderer">The renderer, which answers the size of the window that the interface is laid out into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="renderer"/> is null.</exception>
    public UILayoutSystem(IRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;
    }

    /// <inheritdoc />
    public void UpdateFrame(World world, in GameTime frame)
    {
        ArgumentNullException.ThrowIfNull(world);

        Vector2 screen = _renderer.ViewportSize;

        if (!(screen.X > 0f) || !(screen.Y > 0f))
        {
            return;
        }

        var scale = 1f;
        var resolution = screen;
        var chosen = false;
        var chosenIsRoot = false;

        // Every canvas resolves its own scaler, because a game reads the resolved fields of the canvas it authored, and the one
        // that the anchors of the world are measured against is the root of it.
        foreach (Entity entity in world.Enumerate<CanvasComponent>())
        {
            ref CanvasComponent canvas = ref world.GetRef<CanvasComponent>(entity);
            canvas.Resolve(screen);

            if (!chosen || (!chosenIsRoot && canvas.IsRoot))
            {
                chosen = true;
                chosenIsRoot = canvas.IsRoot;
                scale = canvas.Scale;
                resolution = canvas.Resolution;
            }
        }

        var area = new Rect(Vector2.Zero, resolution);

        foreach (Entity entity in world.Enumerate<RectTransformComponent>())
        {
            ref RectTransformComponent rect = ref world.GetRef<RectTransformComponent>(entity);

            // An element that is not anchored is a rectangle a game wrote down, and the layout leaves it alone.
            if (rect.Anchored)
            {
                rect.Resolve(area, scale);
            }
        }
    }
}
