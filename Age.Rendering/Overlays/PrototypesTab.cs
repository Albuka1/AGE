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
/// whichever of them are there now.
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
    }

    /// <inheritdoc />
    public void Render(IRenderer renderer, Rect body)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        World world = _world();
        List<(string Text, Color Colour)> rows = Rows(world);
        int capacity = Math.Max(1, (int)(body.Height / _line));
        int shown = Math.Min(rows.Count, capacity);
        float y = body.Y;

        for (var index = 0; index < shown; index++)
        {
            (string text, Color colour) = rows[index];
            renderer.DrawText(text, new Vector2(body.X, y), colour);
            y += _line;
        }

        if (shown < rows.Count)
        {
            renderer.DrawText($"... {rows.Count - shown} more", new Vector2(body.X, y), HintColour);
        }
    }

    /// <summary>Builds the lines of the page: a prototype and the entities that came from it, then the entities that came from none.</summary>
    private List<(string Text, Color Colour)> Rows(World world)
    {
        var rows = new List<(string Text, Color Colour)>();
        var ids = _ids().OrderBy(id => id, StringComparer.Ordinal).ToList();

        // The entities of the world are grouped by the prototype they remember, so a prototype with nothing built from it and one with
        // ten things on screen are both read in one place.
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

        rows.Add(($"prototypes {ids.Count}, entities {world.Enumerate().Count()}", HintColour));

        foreach (string id in ids)
        {
            placed.TryGetValue(id, out List<Entity>? list);

            rows.Add(($"{id} x{list?.Count ?? 0}", TextColour));

            foreach (Entity entity in list ?? [])
            {
                rows.Add(($"  {Where(world, entity)}", EntityColour));
            }
        }

        // An entity that came from no prototype is one a game made by hand, which is worth seeing apart from the content.
        if (loose.Count > 0)
        {
            rows.Add(($"(made by hand) x{loose.Count}", TextColour));

            foreach (Entity entity in loose)
            {
                rows.Add(($"  {Where(world, entity)}", EntityColour));
            }
        }

        return rows;
    }

    /// <summary>Returns where an entity stands, which is its position for an entity of the world and its rectangle for an element of the interface.</summary>
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
