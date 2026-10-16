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
/// <para>
/// The service belongs to the first world it is handed, because every map it holds is a set of entity identifiers that
/// name nothing outside the world that created them: another world is refused rather than read through, which is what a
/// game that keeps its windows across a scene load needs to hear about before its buttons reach the wrong entities.
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

    /// <summary>Gets the world this service belongs to, which is the first one it was called with, or null before any call.</summary>
    private World? _owner;

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
    /// <exception cref="InvalidOperationException">No window of the game names that identifier, or the world is not the one this service belongs to.</exception>
    /// <remarks>
    /// A window that is already open is left alone, so a game that opens the window it holds does not build it twice. A
    /// window whose description throws is taken back out of the canvas here rather than being left as a root that no map
    /// names, so a failed open leaves nothing behind for the next frame to draw. A window that names a root that the world
    /// no longer holds is opened again rather than refused: the map of the window is what says it is open, and a root that
    /// a scene destroyed is what a close takes with the elements the map still names.
    /// </remarks>
    public Entity Open(World world, string id)
    {
        world = Own(world);

        if (_open.TryGetValue(id, out Entity existing) && world.IsAlive(existing))
        {
            return existing;
        }

        // The root of a window that the map names is gone, which is what a scene that destroyed it leaves: what the map holds
        // is still what the window built, and every element of it that is alive is what a close takes with it before the build
        // of the window makes the new ones.
        if (_open.ContainsKey(id))
        {
            Close(world, id);
        }

        IUiWindow? window = Find(id)
            ?? throw new InvalidOperationException($"No window of the game names the identifier '{id}'.");

        Entity canvas = Canvas(world);
        Subscribe(world);

        var builder = new UiBuilder(world, canvas);

        try
        {
            window.Build(builder);
        }
        catch
        {
            // The root of the builder is linked into the canvas before the window describes anything, so a window that
            // threw halfway through would leave a root that no `_open` entry names: the canvas would then hold a piece of
            // an interface with no window behind it. The two calls below are the ones a close makes, so the failed build
            // leaves exactly what a window that was never opened leaves.
            if (world.IsAlive(builder.Root.Entity))
            {
                Detach(world, builder.Root.Entity);
                DestroyTree(world, builder.Root.Entity);
            }

            throw;
        }

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
    /// <exception cref="InvalidOperationException">The world is not the one this service belongs to.</exception>
    /// <remarks>
    /// A window that is not open is left alone, so a game that closes the window it does not hold writes nothing. The
    /// root of a window that is closed is taken out of the list of its parent before it is destroyed, because a parent
    /// that still lists it would keep a name that resolves to nothing. A root that a scene destroyed before the window
    /// was closed leaves the elements it built behind it, which the map of the window still names: every one of them that
    /// is alive is queued for destruction here rather than left in the world with no map to find them by.
    /// </remarks>
    public void Close(World world, string id)
    {
        world = Own(world);

        if (!_open.TryGetValue(id, out Entity root))
        {
            return;
        }

        _open.Remove(id);

        Dictionary<string, Entity>? elements = null;
        _elements.Remove(id, out elements);

        if (elements is not null)
        {
            foreach (Entity element in elements.Values)
            {
                _clicks.Remove(element);
            }
        }

        if (world.IsAlive(root))
        {
            Detach(world, root);
            DestroyTree(world, root);
            return;
        }

        // The root of the window is already gone, which is what a scene that destroyed it leaves: the map of the window still
        // names every element it built, and every one of them that is alive is what a close takes with it. What is left in the
        // world after this is what the scene put there itself, which is what a window that is not open leaves behind.
        if (elements is null)
        {
            return;
        }

        foreach (Entity element in elements.Values)
        {
            if (element != root && world.IsAlive(element))
            {
                world.RequestDestroy(element);
            }
        }
    }

    /// <summary>Opens a window that is closed and closes one that is open.</summary>
    /// <param name="world">The world the window stands in.</param>
    /// <param name="id">The identifier of the window.</param>
    /// <exception cref="ArgumentNullException">The world is null.</exception>
    /// <exception cref="InvalidOperationException">The world is not the one this service belongs to.</exception>
    public void Toggle(World world, string id)
    {
        world = Own(world);

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
    /// <exception cref="InvalidOperationException">The world is not the one this service belongs to.</exception>
    public void CloseAll(World world)
    {
        world = Own(world);

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
    /// <exception cref="ArgumentNullException">The world is null.</exception>
    /// <exception cref="InvalidOperationException">The world is not the one this service belongs to.</exception>
    public Entity? Find(World world, string windowId, string elementId)
    {
        world = Own(world);

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
        world = Own(world);

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

    /// <summary>Returns the world this service belongs to, recording it on the first call and refusing any other one.</summary>
    /// <param name="world">The world a caller is about to read or write through.</param>
    /// <returns>The world, which is the one the service was first called with.</returns>
    /// <exception cref="ArgumentNullException">The world is null.</exception>
    /// <exception cref="InvalidOperationException">The world is not the one this service belongs to.</exception>
    private World Own(World? world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (_owner is null)
        {
            _owner = world;
            return world;
        }

        if (!ReferenceEquals(_owner, world))
        {
            throw new InvalidOperationException(
                "This window service belongs to the world it was first called with, and it cannot read the elements of another one: a service of its own is what another world needs.");
        }

        return world;
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

    /// <summary>Takes an element out of the list of the parent it stands inside.</summary>
    /// <param name="world">The world the element lives in.</param>
    /// <param name="entity">The element to take out.</param>
    private static void Detach(World world, Entity entity)
    {
        if (!world.Has<ParentComponent>(entity))
        {
            return;
        }

        Entity parent = world.Resolve(world.Get<ParentComponent>(entity).Parent);

        if (!world.IsAlive(parent) || !world.Has<ChildrenComponent>(parent))
        {
            return;
        }

        ref ChildrenComponent children = ref world.GetRef<ChildrenComponent>(parent);

        if (children.Children is not { Length: > 0 } current)
        {
            return;
        }

        int kept = 0;

        foreach (EntityRef reference in current)
        {
            if (world.Resolve(reference) != entity)
            {
                kept++;
            }
        }

        if (kept == current.Length)
        {
            return;
        }

        EntityRef[] updated = new EntityRef[kept];
        var index = 0;

        foreach (EntityRef reference in current)
        {
            if (world.Resolve(reference) != entity)
            {
                updated[index++] = reference;
            }
        }

        children.Children = updated;
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
