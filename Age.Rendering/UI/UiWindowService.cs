using Age.Core;
using Age.UI;

namespace Age.Rendering;

/// <summary>
/// Holds every window of a game and the elements of the ones that are open: it builds a window when it is opened, reads
/// the elements back by name, and dispatches a press of a button to what the window that built it said.
/// </summary>
/// <remarks>
/// <para>
/// The service is a frame system, so a window that is open is updated once per frame from the render callback of the
/// loop: a window reads its elements and writes what they show, and a window that reacts to a key or a click of its own
/// reads the input of the frame rather than holding a subscription of its own. The canvas is shared by every window that
/// is open, and it is the game's root canvas when the game already made one.
/// </para>
/// <para>
/// A window that is closed is destroyed: the entities of its elements are handed to <see cref="World.RequestDestroy"/> at
/// the boundary of the step, and a window that is opened again builds its elements again. What a window keeps between
/// two opens is what a game puts on the class of the window itself, which is what a class with an interface is for.
/// </para>
/// </remarks>
[System]
public sealed class UiWindowService : IFrameSystem
{
    private readonly IUiWindow[] _windows;
    private readonly Dictionary<string, Entity> _open = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, Entity>> _elements = new(StringComparer.Ordinal);
    private readonly Dictionary<Entity, Action> _clicks = new();

    private Entity _canvas;
    private World? _subscribed;

    /// <summary>Initializes the service with every window the container holds.</summary>
    /// <param name="windows">The windows, which a scan of an assembly fills from what its classes say.</param>
    /// <exception cref="ArgumentNullException">The windows are null.</exception>
    public UiWindowService(IEnumerable<IUiWindow> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);
        _windows = [.. windows];
    }

    /// <summary>Gets the identifiers of the windows the container holds, in the order they were registered.</summary>
    public IReadOnlyList<string> Ids => [.. _windows.Select(window => window.Id)];

    /// <summary>Returns a value indicating whether a window is open.</summary>
    /// <param name="id">The identifier of the window.</param>
    /// <returns><see langword="true"/> when the window is open.</returns>
    public bool IsOpen(string id) => _open.ContainsKey(id);

    /// <summary>Opens a window and builds what it holds.</summary>
    /// <param name="world">The world the window stands in, which the entities of its elements are created in.</param>
    /// <param name="id">The identifier of the window, as the class of it says.</param>
    /// <returns>The entity of the root panel of the window.</returns>
    /// <exception cref="ArgumentNullException">The world is null.</exception>
    /// <exception cref="InvalidOperationException">No window of the game names that identifier.</exception>
    /// <remarks>A window that is already open is left alone, so a game that opens the window it holds does not build it twice.</remarks>
    public Entity Open(World world, string id)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (_open.TryGetValue(id, out Entity existing) && world.IsAlive(existing))
        {
            return existing;
        }

        IUiWindow? window = Find(id)
            ?? throw new InvalidOperationException($"No window of the game names the identifier '{id}'.");

        Entity canvas = Canvas(world);
        Subscribe(world);

        var builder = new UiBuilder(world, canvas);
        window.Build(builder);

        _open[id] = builder.Root.Entity;
        _elements[id] = new Dictionary<string, Entity>(builder.Elements, StringComparer.Ordinal);

        foreach ((Entity button, Action action) in builder.Clicks)
        {
            _clicks[button] = action;
        }

        return builder.Root.Entity;
    }

    /// <summary>Closes a window and destroys the entities of its elements.</summary>
    /// <param name="world">The world the window stands in.</param>
    /// <param name="id">The identifier of the window.</param>
    /// <exception cref="ArgumentNullException">The world is null.</exception>
    /// <remarks>A window that is not open is left alone, so a game that closes the window it does not hold writes nothing.</remarks>
    public void Close(World world, string id)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!_open.TryGetValue(id, out Entity root))
        {
            return;
        }

        _open.Remove(id);

        if (_elements.Remove(id, out Dictionary<string, Entity>? elements))
        {
            foreach (Entity element in elements.Values)
            {
                _clicks.Remove(element);
            }
        }

        if (world.IsAlive(root))
        {
            DestroyTree(world, root);
        }
    }

    /// <summary>Opens a window that is closed and closes one that is open.</summary>
    /// <param name="world">The world the window stands in.</param>
    /// <param name="id">The identifier of the window.</param>
    /// <exception cref="ArgumentNullException">The world is null.</exception>
    public void Toggle(World world, string id)
    {
        if (IsOpen(id))
        {
            Close(world, id);
        }
        else
        {
            Open(world, id);
        }
    }

    /// <summary>Closes every window that is open.</summary>
    /// <param name="world">The world the windows stand in.</param>
    /// <exception cref="ArgumentNullException">The world is null.</exception>
    public void CloseAll(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        foreach (string id in _open.Keys.ToArray())
        {
            Close(world, id);
        }
    }

    /// <summary>Returns the entity of an element that a window built, or null when the window holds no element of that name.</summary>
    /// <param name="world">The world the window stands in.</param>
    /// <param name="windowId">The identifier of the window.</param>
    /// <param name="elementId">The name the element was built under.</param>
    /// <returns>The entity of the element, or null.</returns>
    public Entity? Find(World world, string windowId, string elementId)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!_elements.TryGetValue(windowId, out Dictionary<string, Entity>? map))
        {
            return null;
        }

        return map.TryGetValue(elementId, out Entity entity) && world.IsAlive(entity) ? entity : null;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The windows are read into a buffer before they are updated, because a window that closes itself or another one
    /// while it is being updated changes the map that is being read.
    /// </remarks>
    public void UpdateFrame(World world, in GameTime frame)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (_open.Count == 0)
        {
            return;
        }

        KeyValuePair<string, Entity>[] snapshot = [.. _open];

        foreach ((string id, Entity canvas) in snapshot)
        {
            if (!world.IsAlive(canvas))
            {
                continue;
            }

            if (Find(id) is IUiWindow window)
            {
                window.Update(new UiWindowContext(world, this, id, canvas));
            }
        }
    }

    private IUiWindow? Find(string id)
    {
        foreach (IUiWindow window in _windows)
        {
            if (string.Equals(window.Id, id, StringComparison.Ordinal))
            {
                return window;
            }
        }

        return null;
    }

    private void Subscribe(World world)
    {
        if (ReferenceEquals(_subscribed, world))
        {
            return;
        }

        _subscribed = world;
        world.Events.Subscribe<ButtonPressedEvent>(OnButtonPressed);
    }

    private void OnButtonPressed(Entity _, ButtonPressedEvent @event)
    {
        if (_clicks.TryGetValue(@event.Button, out Action? handler))
        {
            handler();
        }
    }

    private Entity Canvas(World world)
    {
        if (_canvas != default && world.IsAlive(_canvas))
        {
            return _canvas;
        }

        foreach (Entity entity in world.Enumerate<CanvasComponent>())
        {
            if (world.Get<CanvasComponent>(entity).IsRoot)
            {
                _canvas = entity;
                return _canvas;
            }
        }

        _canvas = world.CreateEntity();
        world.Set(_canvas, new CanvasComponent
        {
            IsRoot = true,
            DesignSize = new Vector2(1280f, 720f),
            ScaleMode = CanvasScaleMode.ScaleWithScreenSize,
            Match = 0.5f,
        });

        return _canvas;
    }

    private static void DestroyTree(World world, Entity entity)
    {
        if (world.Has<ChildrenComponent>(entity))
        {
            foreach (EntityRef reference in world.Get<ChildrenComponent>(entity).Children ?? [])
            {
                Entity child = reference.Resolve(world);

                if (child != default(Entity) && world.IsAlive(child))
                {
                    DestroyTree(world, child);
                }
            }
        }

        world.RequestDestroy(entity);
    }
}
