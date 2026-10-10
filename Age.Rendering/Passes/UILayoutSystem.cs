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

        // The tree is walked from its roots downwards, so a parent is resolved before its children are: a child that is laid out needs
        // the rectangle its parent ended up with, and a parent that is still waiting for one would lay its child out against the
        // canvas. An element that names no parent is a root and is laid out against the canvas; an element whose parent is not in the
        // world, or whose parent is not an element, is a root too rather than standing nowhere. Every element is visited once, so a
        // cycle in the tree does not walk forever.
        var visited = new HashSet<Entity>();

        foreach (Entity entity in world.Enumerate<RectTransformComponent>())
        {
            Entity parent = ParentOf(world, entity);

            // An element is a root when it names no parent, when the parent is not in the world, or when the parent is not itself an
            // element: a canvas is what a tree of the interface hangs from, and it is placed by the scaler rather than by an anchor, so
            // its children are laid out against the canvas rather than against a rectangle it does not have.
            if (!world.IsAlive(parent) || !world.Has<RectTransformComponent>(parent))
            {
                Layout(world, entity, area, scale, visited);
            }
        }

        // An element that no root reached — it is part of a cycle — is laid out against the canvas, so it is in a sensible place
        // rather than left where a previous frame put it.
        foreach (Entity entity in world.Enumerate<RectTransformComponent>())
        {
            if (visited.Add(entity))
            {
                Resolve(world, entity, area, scale);
            }
        }
    }

    /// <summary>Returns the element that an element stands inside, or the default entity when it is a root or names none.</summary>
    private static Entity ParentOf(World world, Entity entity) =>
        world.Has<ParentComponent>(entity) ? world.Resolve(world.Get<ParentComponent>(entity).Parent) : default;

    /// <summary>Resolves an element and then the elements that stand inside it, which is what a parent-first walk is.</summary>
    /// <param name="world">The world that holds the tree.</param>
    /// <param name="entity">The element to resolve.</param>
    /// <param name="parent">The rectangle the element is placed in: the canvas for a root, the parent for a child.</param>
    /// <param name="scale">The scale of the canvas, which is how many pixels of the screen a design unit is.</param>
    /// <param name="visited">The elements that were resolved, which is what keeps a cycle from walking forever.</param>
    private static void Layout(World world, Entity entity, Rect parent, float scale, HashSet<Entity> visited)
    {
        if (!world.IsAlive(entity) || !visited.Add(entity))
        {
            return;
        }

        Rect rect = Resolve(world, entity, parent, scale);

        if (!world.Has<ChildrenComponent>(entity))
        {
            return;
        }

        foreach (EntityRef reference in world.Get<ChildrenComponent>(entity).Children ?? [])
        {
            Layout(world, world.Resolve(reference), rect, scale, visited);
        }
    }

    /// <summary>Resolves the rectangle of one element inside the rectangle it is placed in, and answers the rectangle it ended up with.</summary>
    private static Rect Resolve(World world, Entity entity, Rect parent, float scale)
    {
        ref RectTransformComponent rect = ref world.GetRef<RectTransformComponent>(entity);

        // An element that is not anchored is a rectangle a game wrote down, and the layout leaves it alone.
        if (rect.Anchored)
        {
            rect.Resolve(parent, scale);
        }

        return new Rect(rect.Position, rect.Size);
    }
}
