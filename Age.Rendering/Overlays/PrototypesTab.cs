using Age.Core;

namespace Age.Rendering;

/// <summary>
/// The page of a developer window that gathers the prototypes of a run: every prototype the content registered, and where each is
/// placed in the world, which is the entities built from it with their position.
/// </summary>
/// <remarks>
/// <para>
/// The page answers "where did this thing come from" without a debugger: an entity a game spawned from a prototype remembers the
/// identifier it was made from, so the page groups the entities of the world by that identifier and prints each group under its
/// prototype. An entity that was built by hand rather than spawned is listed under the name that says so.
/// </para>
/// <para>
/// The world is read through a delegate rather than held, because a game replaces its world when it loads a scene, and the list of
/// prototype identifiers is a delegate too, because the content belongs to the game rather than to the renderer: the page reports
/// whichever of them are there now. A run of any length has more prototypes and entities than the body of the page shows, so a wheel
/// over the body scrolls it and a bar at the right edge says where a person stands in it.
/// </para>
/// </remarks>
public sealed class PrototypesTab : IDevWindowTab
{
    private static readonly Color TextColour = new(225, 225, 225);
    private static readonly Color HintColour = new(150, 150, 150);
    private static readonly Color EntityColour = new(180, 200, 230);

    private readonly Func<IEnumerable<string>> _ids;
    private readonly Func<World> _world;
    private readonly float _line = BitmapFontMetrics.GlyphHeight + 4f;
    private readonly List<(string Text, Color Colour)> _rows = [];

    private ScrollState _scroll;

    /// <summary>Initializes the page with the prototypes it reports and the world whose entities came from them.</summary>
    /// <param name="ids">The identifiers of the prototypes the content registered, asked for on every frame.</param>
    /// <param name="world">The world to read, which is asked for on every frame so a scene that replaced it is reported.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public PrototypesTab(Func<IEnumerable<string>> ids, Func<World> world)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(world);

        _ids = ids;
        _world = world;
    }

    /// <inheritdoc />
    public string Title => "prototypes";

    /// <inheritdoc />
    public bool Closable => true;

    /// <inheritdoc />
    public void Update(in GameTime frame, Rect body, in WindowPointer pointer)
    {
        Rows(_world());
        _scroll.Update(pointer, body, _rows.Count, _line);
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
                (string text, Color colour) = _rows[start + index];
                renderer.DrawText(text, new Vector2(body.X, y), colour);
                y += _line;
            }
        }
        finally
        {
            renderer.PopClip();
        }

        _scroll.DrawBar(renderer, body, _rows.Count, _line);
    }

    /// <summary>Builds the lines of the page: a prototype and the entities that came from it, then the entities that came from none.</summary>
    private void Rows(World world)
    {
        _rows.Clear();

        var ids = _ids().OrderBy(id => id, StringComparer.Ordinal).ToList();

        var placed = new Dictionary<string, List<Entity>>(StringComparer.Ordinal);
        var loose = new List<Entity>();

        foreach (Entity entity in world.Enumerate())
        {
            string? id = world.PrototypeOf(entity);

            if (id is null)
            {
                loose.Add(entity);
                continue;
            }

            if (!placed.TryGetValue(id, out List<Entity>? list))
            {
                list = [];
                placed[id] = list;
            }

            list.Add(entity);
        }

        _rows.Add(($"prototypes {ids.Count}, entities {world.Enumerate().Count()}", HintColour));

        foreach (string id in ids)
        {
            placed.TryGetValue(id, out List<Entity>? list);

            _rows.Add(($"{id} x{list?.Count ?? 0}", TextColour));

            foreach (Entity entity in list ?? [])
            {
                _rows.Add(($"  {Where(world, entity)}", EntityColour));
            }
        }

        if (loose.Count > 0)
        {
            _rows.Add(($"(made by hand) x{loose.Count}", TextColour));

            foreach (Entity entity in loose)
            {
                _rows.Add(($"  {Where(world, entity)}", EntityColour));
            }
        }
    }

    private static string Where(World world, Entity entity)
    {
        if (world.Has<TransformComponent>(entity))
        {
            Vector2 position = world.Get<TransformComponent>(entity).Position;
            return $"#{entity.Id} {position.X:0},{position.Y:0}";
        }

        return $"#{entity.Id}";
    }
}
