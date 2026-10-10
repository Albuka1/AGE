using Age.Core;
using Age.UI;

namespace Age.Rendering;

/// <summary>
/// The page of a developer window that shows the interface as a tree: every element of the canvas, its children under it, and the
/// rectangle each one was laid out at.
/// </summary>
/// <remarks>
/// <para>
/// The tree is what a flat list cannot show: an element that stands inside a panel is printed under it, one level deeper, so a person
/// reads where a button belongs rather than a list of rectangles that happen to overlap. An element whose parent names an entity the
/// world does not hold is printed as a root, which is what it is laid out as.
/// </para>
/// <para>
/// The world is read through a delegate rather than held, because a game replaces its world when it loads a scene: the page reports the
/// world that is there now rather than one that was there when the page was registered.
/// </para>
/// </remarks>
public sealed class UITreeTab : IDevWindowTab
{
    private static readonly Color TextColour = new(225, 225, 225);
    private static readonly Color HintColour = new(150, 150, 150);
    private static readonly Color LeafColour = new(180, 200, 230);

    private readonly Func<World> _world;
    private readonly float _line = BitmapFontMetrics.GlyphHeight + 4f;

    /// <summary>Initializes the page with the world it reports.</summary>
    /// <param name="world">The world to read, which is asked for on every frame so a scene that replaced it is reported.</param>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is null.</exception>
    public UITreeTab(Func<World> world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
    }

    /// <inheritdoc />
    public string Title => "ui";

    /// <inheritdoc />
    /// <remarks>What this page reports is read from the world on every frame, so it reads no pointer of its own.</remarks>
    public bool Closable => true;

    /// <inheritdoc />
    public void Update(in GameTime frame, Rect body, in WindowPointer pointer)
    {
    }

    /// <inheritdoc />
    public void Render(IRenderer renderer, Rect body)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        World world = _world();
        List<(int Depth, Entity Entity)> rows = Tree(world);
        int capacity = Math.Max(1, (int)(body.Height / _line));

        // The tree is longer than the body more often than not, so the rows that fit are the ones at the top: the roots and what
        // stands under them are what a person is looking for, and the count of what is not shown is printed at the end.
        int shown = Math.Min(rows.Count, capacity);
        float y = body.Y;

        for (var index = 0; index < shown; index++)
        {
            (int depth, Entity entity) = rows[index];
            string indent = new(' ', depth * 2);

            renderer.DrawText($"{indent}{Describe(world, entity)}", new Vector2(body.X, y), depth == 0 ? TextColour : LeafColour);
            y += _line;
        }

        if (rows.Count == 0)
        {
            renderer.DrawText("(the interface holds no element)", new Vector2(body.X, body.Y), HintColour);
        }
        else if (shown < rows.Count)
        {
            renderer.DrawText($"... {rows.Count - shown} more", new Vector2(body.X, y), HintColour);
        }
    }

    /// <summary>Walks the interface from its roots downwards, which is the order the tree is printed in.</summary>
    /// <param name="world">The world to read.</param>
    /// <returns>Every element with the depth it stands at, a root at zero.</returns>
    /// <remarks>Every element is visited once, so a cycle does not walk forever, and an element no root reached is printed as a root of its own.</remarks>
    private static List<(int Depth, Entity Entity)> Tree(World world)
    {
        var rows = new List<(int Depth, Entity Entity)>();
        var visited = new HashSet<Entity>();

        foreach (Entity entity in world.Enumerate<RectTransformComponent>())
        {
            Entity parent = world.Has<ParentComponent>(entity) ? world.Resolve(world.Get<ParentComponent>(entity).Parent) : default;

            if (!world.IsAlive(parent) || !world.Has<RectTransformComponent>(parent))
            {
                Walk(world, entity, 0, visited, rows);
            }
        }

        foreach (Entity entity in world.Enumerate<RectTransformComponent>())
        {
            if (visited.Add(entity))
            {
                Walk(world, entity, 0, visited, rows);
            }
        }

        return rows;
    }

    /// <summary>Adds an element and the elements under it to the rows of the tree, one level deeper.</summary>
    private static void Walk(World world, Entity entity, int depth, HashSet<Entity> visited, List<(int Depth, Entity Entity)> rows)
    {
        if (!world.IsAlive(entity) || !visited.Add(entity))
        {
            return;
        }

        rows.Add((depth, entity));

        if (!world.Has<ChildrenComponent>(entity))
        {
            return;
        }

        foreach (EntityRef reference in world.Get<ChildrenComponent>(entity).Children ?? [])
        {
            Walk(world, world.Resolve(reference), depth + 1, visited, rows);
        }
    }

    /// <summary>Returns the line a person reads for one element: what it is and where it stands.</summary>
    private static string Describe(World world, Entity entity)
    {
        RectTransformComponent rect = world.Get<RectTransformComponent>(entity);
        string name = Name(world, entity);
        string text = rect.Visible ? string.Empty : " (hidden)";

        return $"{name} {rect.Position.X:0},{rect.Position.Y:0} {rect.Size.X:0}x{rect.Size.Y:0}{text}";
    }

    /// <summary>Returns what an element is: a button, a canvas, a panel, or the number of the entity that carries nothing known.</summary>
    private static string Name(World world, Entity entity)
    {
        if (world.Has<ButtonComponent>(entity))
        {
            return "button";
        }

        var parts = new List<string>();

        if (world.Has<CanvasComponent>(entity))
        {
            parts.Add("canvas");
        }

        if (world.Has<ChildrenComponent>(entity))
        {
            parts.Add("panel");
        }

        return parts.Count > 0 ? string.Join('+', parts) : $"#{entity.Id}";
    }
}
