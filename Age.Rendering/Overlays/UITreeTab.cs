using Age.Core;
using Age.UI;

namespace Age.Rendering;

/// <summary>
/// The page of a developer window that shows the interface as a tree: every element of the canvas, its children under
/// it, and the rectangle each one was laid out at.
/// </summary>
/// <remarks>
/// <para>
/// The tree is what a flat list cannot show: an element that stands inside a panel is printed under it, one level deeper,
/// so a person reads where a button belongs rather than a list of rectangles that happen to overlap. An element whose
/// parent names an entity the world does not hold is printed as a root, which is what it is laid out as.
/// </para>
/// <para>
/// The tree is walked on every frame, because what it shows is what the world holds right now: a window that opened, an
/// element that was hidden, an entity that was destroyed. What is remembered between two frames is only the elements a
/// person folded away, which is a property of what they are looking at rather than of the world.
/// </para>
/// <para>
/// The rows are longer than the body of the window more often than not, so the page scrolls: a wheel over the body moves
/// the rows, and a bar at the right edge shows where a person stands in them. A click on a row that has children folds it
/// away or opens it, which is what a tree is read with when the interface of a window is deeper than the room of the page.
/// </para>
/// </remarks>
public sealed class UITreeTab : IDevWindowTab
{
    private static readonly Color TextColour = new(225, 225, 225);
    private static readonly Color HintColour = new(150, 150, 150);
    private static readonly Color LeafColour = new(180, 200, 230);
    private static readonly Color ToggleColour = new(255, 210, 90);

    private readonly Func<World> _world;
    private readonly float _line = BitmapFontMetrics.GlyphHeight + 4f;
    private readonly List<Row> _rows = [];
    private readonly HashSet<Entity> _collapsed = [];

    private World? _lastWorld;
    private ScrollState _scroll;

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
    public bool Closable => true;

    /// <inheritdoc />
    public void Update(in GameTime frame, Rect body, in WindowPointer pointer)
    {
        World world = _world();

        if (!ReferenceEquals(_lastWorld, world))
        {
            _lastWorld = world;
            _collapsed.Clear();
            _scroll = default;
        }

        Build(world);
        _scroll.Update(pointer, body, _rows.Count, _line);

        if (!pointer.Pressed || !Contains(body, pointer.Position))
        {
            return;
        }

        int row = _scroll.First + (int)((pointer.Position.Y - body.Y) / _line);

        if (row < 0 || row >= _rows.Count)
        {
            return;
        }

        Row clicked = _rows[row];

        if (!clicked.HasChildren)
        {
            return;
        }

        if (!_collapsed.Remove(clicked.Entity))
        {
            _collapsed.Add(clicked.Entity);
        }
    }

    /// <inheritdoc />
    public void Render(IRenderer renderer, Rect body)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        int visible = ScrollState.Visible(body, _line);
        int count = Math.Min(_rows.Count, visible);
        int start = Math.Clamp(_scroll.First, 0, Math.Max(0, _rows.Count - count));

        renderer.PushClip(body);

        try
        {
            float y = body.Y;

            for (var index = 0; index < count; index++)
            {
                Row row = _rows[start + index];
                Color colour = row.HasChildren ? TextColour : LeafColour;

                if (row.HasChildren)
                {
                    renderer.DrawText(row.Marker, new Vector2(body.X, y), ToggleColour);
                    renderer.DrawText(row.Text, new Vector2(body.X + (BitmapFontMetrics.GlyphWidth * row.Marker.Length), y), colour);
                }
                else
                {
                    renderer.DrawText(row.Text, new Vector2(body.X, y), colour);
                }

                y += _line;
            }

            if (_rows.Count == 0)
            {
                renderer.DrawText("(the interface holds no element)", new Vector2(body.X, body.Y), HintColour);
            }
        }
        finally
        {
            renderer.PopClip();
        }

        _scroll.DrawBar(renderer, body, _rows.Count, _line);
    }

    private void Build(World world)
    {
        _rows.Clear();

        var visited = new HashSet<Entity>();

        foreach (Entity entity in world.Enumerate<RectTransformComponent>())
        {
            Entity parent = world.Has<ParentComponent>(entity) ? world.Resolve(world.Get<ParentComponent>(entity).Parent) : default;

            if (!world.IsAlive(parent) || !world.Has<RectTransformComponent>(parent))
            {
                Walk(world, entity, prefix: string.Empty, isRoot: true, isLast: true, visited);
            }
        }

        foreach (Entity entity in world.Enumerate<RectTransformComponent>())
        {
            Walk(world, entity, prefix: string.Empty, isRoot: true, isLast: true, visited);
        }
    }

    private void Walk(World world, Entity entity, string prefix, bool isRoot, bool isLast, HashSet<Entity> visited)
    {
        if (!world.IsAlive(entity) || !visited.Add(entity))
        {
            return;
        }

        List<Entity> children = UnvisitedChildren(world, entity, visited);
        bool hasChildren = children.Count > 0;
        bool expanded = !_collapsed.Contains(entity);

        string connector = isRoot ? string.Empty : isLast ? "\\ " : "| ";
        string marker = hasChildren ? (expanded ? "v " : "> ") : "- ";

        _rows.Add(new Row(entity, connector + marker, prefix + connector + marker + Describe(world, entity), hasChildren));

        if (!hasChildren || !expanded)
        {
            return;
        }

        string childPrefix = prefix + (isRoot ? string.Empty : isLast ? "  " : "| ");

        for (var index = 0; index < children.Count; index++)
        {
            Walk(world, children[index], childPrefix, isRoot: false, isLast: index == children.Count - 1, visited);
        }
    }

    private static List<Entity> UnvisitedChildren(World world, Entity entity, HashSet<Entity> visited)
    {
        var children = new List<Entity>();

        if (!world.Has<ChildrenComponent>(entity))
        {
            return children;
        }

        foreach (EntityRef reference in world.Get<ChildrenComponent>(entity).Children ?? [])
        {
            Entity child = world.Resolve(reference);

            if (world.IsAlive(child) && !visited.Contains(child) && world.Has<RectTransformComponent>(child))
            {
                children.Add(child);
            }
        }

        return children;
    }

    private static string Describe(World world, Entity entity)
    {
        RectTransformComponent rect = world.Get<RectTransformComponent>(entity);
        string name = Name(world, entity);
        string text = rect.Visible ? string.Empty : " (hidden)";

        return $"{name} {rect.Position.X:0},{rect.Position.Y:0} {rect.Size.X:0}x{rect.Size.Y:0}{text}";
    }

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

    private static bool Contains(Rect rect, Vector2 point) =>
        point.X >= rect.X && point.X < rect.X + rect.Width && point.Y >= rect.Y && point.Y < rect.Y + rect.Height;

    private readonly record struct Row(Entity Entity, string Marker, string Text, bool HasChildren);
}
