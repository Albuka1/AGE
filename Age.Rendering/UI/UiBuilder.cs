using Age.Core;
using Age.UI;

namespace Age.Rendering;

/// <summary>
/// Describes one window of an interface: a builder that creates the entities of a window and wires them into a tree.
/// </summary>
/// <remarks>
/// <para>
/// The builder creates one entity per element, sets the components of the interface on it, and links it into the tree of
/// its parent: a child names its parent in <see cref="ParentComponent"/> and a parent lists its children in
/// <see cref="ChildrenComponent"/>, which is what the layout of the interface and the page of a developer read. A name
/// that a window gives to an element is written into <see cref="UiElementComponent"/> and is what the window reads the
/// element back by, so a window never holds an identifier of an entity between two frames.
/// </para>
/// <para>
/// The panel of a window is what the block of a <c>using</c> makes: <see cref="Panel(string, Action{PanelHandle})"/>
/// returns a scope that puts the panel on the top of the builder's stack and takes it off when the scope is disposed,
/// which is what a nested description reads like. A panel built without a block leaves the stack where it was, so a game
/// that writes its elements one after another does not need to nest them.
/// </para>
/// <para>
/// A group is what a window reaches for when the elements of a card or a list are arranged inside a rectangle that
/// nothing is drawn in: a panel whose colour is transparent draws nothing but is still an element of the pass, and a
/// group is not an element of the pass at all, so a window that only wants to place elements against a rectangle uses
/// the group and keeps the pass from drawing a rectangle of no colour.
/// </para>
/// <para>
/// A zone is what a game reaches for when the interface of a window is a picture of its own: it is an element that is
/// laid out and takes the pointer like every other one, and the frame that draws it draws nothing, because the face of a
/// button with no alpha and the frame of one with no colour are the two that a pass skips. What a person sees of such a
/// window is the picture under the zones rather than the faces of them.
/// </para>
/// </remarks>
public sealed class UiBuilder
{
    private readonly World _world;
    private readonly Dictionary<string, Entity> _elements = new(StringComparer.Ordinal);
    private readonly Dictionary<Entity, Action> _clicks = new();
    private readonly Stack<Entity> _parents = new();

    internal UiBuilder(World world, Entity canvas)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;

        Entity root = CreateCanvasChild(canvas, "root");

        // A canvas is drawn as a panel with no colour of its own, so a window that colours its root gives the canvas a
        // visible rectangle rather than covering the room of a canvas a game already made. The alpha of zero is what
        // makes the root a place to stand rather than a picture.
        _world.Set(root, new PanelComponent { Color = new Color(0, 0, 0) { A = 0 } });

        // The root is named like every other element, so a window reads it back to hide or show the whole tree when the
        // mode of a run changes: a tree is drawn by its own elements, and the pass of the interface walks the parents of
        // an element, so a root that is not visible takes everything under it with it.
        _elements["root"] = root;

        Root = new PanelHandle(world, root);
        _parents.Push(root);
    }

    /// <summary>Gets the panel that holds every element of the window, which is what a window sizes and colours first.</summary>
    public PanelHandle Root { get; }

    /// <summary>Gets the elements the window built, by the name each of them was built under.</summary>
    public IReadOnlyDictionary<string, Entity> Elements => _elements;

    /// <summary>Gets the handlers of the buttons the window built, by the button that answers the press.</summary>
    internal IReadOnlyDictionary<Entity, Action> Clicks => _clicks;

    /// <summary>Creates a panel under the current parent and returns it.</summary>
    /// <param name="id">The name of the element inside the window.</param>
    /// <returns>The handle of the panel.</returns>
    /// <exception cref="ArgumentException">The name is null, empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The window already holds an element of that name.</exception>
    public PanelHandle Panel(string id)
    {
        Entity entity = Add(id, Parent);
        _world.Set(entity, new PanelComponent());
        return new PanelHandle(_world, entity);
    }

    /// <summary>Creates a panel under the current parent, configures it, and makes it the parent of the elements of the block.</summary>
    /// <param name="id">The name of the element inside the window.</param>
    /// <param name="configure">The moves that place the panel and give it its colour.</param>
    /// <returns>A scope that returns the parent of the builder to what it was when the block ends.</returns>
    public PanelScope Panel(string id, Action<PanelHandle> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        PanelHandle handle = Panel(id);
        configure(handle);
        _parents.Push(handle.Entity);

        return new PanelScope(this);
    }

    /// <summary>Creates a container that holds other elements and draws nothing.</summary>
    /// <param name="id">The name of the element inside the window.</param>
    /// <returns>The handle of the group.</returns>
    /// <exception cref="ArgumentException">The name is null, empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The window already holds an element of that name.</exception>
    /// <remarks>
    /// A group is a rectangle that no pass of the interface reads: what the elements under it are laid out against is
    /// its room, and what a person sees is the elements themselves and nothing of the group. A window that wants the
    /// frame of a card behind a list of rows puts a panel and a group over each other, rather than a panel behind a
    /// panel whose inside is a second frame.
    /// </remarks>
    public GroupHandle Group(string id)
    {
        Entity entity = Add(id, Parent);
        return new GroupHandle(_world, entity);
    }

    /// <summary>Creates a group under the current parent, configures it, and makes it the parent of the elements of the block.</summary>
    /// <param name="id">The name of the element inside the window.</param>
    /// <param name="configure">The moves that place the group and size it.</param>
    /// <returns>A scope that returns the parent of the builder to what it was when the block ends.</returns>
    public PanelScope Group(string id, Action<GroupHandle> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        GroupHandle handle = Group(id);
        configure(handle);
        _parents.Push(handle.Entity);

        return new PanelScope(this);
    }

    /// <summary>Creates a label under the current parent.</summary>
    /// <param name="id">The name of the element inside the window.</param>
    /// <returns>The handle of the label.</returns>
    public LabelHandle Label(string id) => new(_world, AddLabel(id));

    /// <summary>Creates a label under the current parent, configured by the caller.</summary>
    /// <param name="id">The name of the element inside the window.</param>
    /// <param name="configure">The moves that write the line and place it.</param>
    /// <returns>The handle of the label.</returns>
    public LabelHandle Label(string id, Action<LabelHandle> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        LabelHandle handle = Label(id);
        configure(handle);
        return handle;
    }

    /// <summary>Creates a button under the current parent.</summary>
    /// <param name="id">The name of the element inside the window.</param>
    /// <returns>The handle of the button.</returns>
    /// <remarks>
    /// A button carries a text of its own, centred in the box of its rectangle, which is what <see cref="ButtonHandle.Text"/>
    /// writes: a button that names no text of its own is drawn as a face of a colour, and one that writes a caption draws
    /// the caption over that face. The face is given a visible colour here, because the default of the structure is
    /// transparent and a button that never writes one would be drawn as nothing: a window that wants another colour
    /// changes it with <see cref="ButtonHandle.Color"/>.
    /// </remarks>
    public ButtonHandle Button(string id)
    {
        Entity entity = Add(id, Parent);
        _world.Set(entity, new ButtonComponent
        {
            BaseColor = new Color(44, 48, 60),
            Interactable = true,
        });
        _world.Set(entity, new TextComponent
        {
            Style = new TextStyle
            {
                Color = new Color(225, 225, 225),
                Align = TextAlign.Center,
                VerticalAlign = TextVerticalAlign.Middle,
            },
        });
        return new ButtonHandle(this, _world, entity);
    }

    /// <summary>Creates a button under the current parent, configured by the caller.</summary>
    /// <param name="id">The name of the element inside the window.</param>
    /// <param name="configure">The moves that write the caption of the button, place it, and set what a press does.</param>
    /// <returns>The handle of the button.</returns>
    public ButtonHandle Button(string id, Action<ButtonHandle> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ButtonHandle handle = Button(id);
        configure(handle);
        return handle;
    }

    /// <summary>Creates a rectangle that answers the pointer and draws nothing, which is what a zone over a picture of the interface is.</summary>
    /// <param name="id">The name of the element inside the window.</param>
    /// <returns>The handle of the zone.</returns>
    /// <exception cref="ArgumentException">The name is null, empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The window already holds an element of that name.</exception>
    /// <remarks>
    /// A zone is what a game reaches for when the interface of a window is a picture of its own: an image, a card, a portrait
    /// drawn as a sprite of the world rather than as a rectangle of the interface, and the zones stand over it to say what a
    /// click on each part of it is. The element is laid out and takes the pointer like every other one of the interface, and
    /// the frame that draws it draws nothing, because the face of a button with no alpha and the frame of one with no colour
    /// are the two that a pass skips.
    /// </remarks>
    /// <example>
    /// <code>
    /// // The card of a character is a picture of its own, and the three zones over it are what a person presses:
    /// ui.Image("character").Source("Textures/Ui/character-card.png").At(0f, 0f).Size(400f, 200f);
    ///
    /// ui.Zone("head").At(20f, 20f).Size(80f, 80f).OnClick(() => Select(Slot.Head));
    /// ui.Zone("hands").At(20f, 100f).Size(80f, 40f).OnClick(() => Select(Slot.Hands));
    /// ui.Zone("belt").At(300f, 100f).Size(80f, 40f).OnClick(() => Select(Slot.Belt));
    /// </code>
    /// </example>
    public ButtonHandle Zone(string id)
    {
        Entity entity = Add(id, Parent);
        _world.Set(entity, new ButtonComponent
        {
            BaseColor = new Color(0, 0, 0) { A = 0 },
            BorderColor = new Color(0, 0, 0) { A = 0 },
            BorderThickness = 0,
            Interactable = true,
        });

        return new ButtonHandle(this, _world, entity);
    }

    /// <summary>Creates a zone under the current parent, configured by the caller.</summary>
    /// <param name="id">The name of the element inside the window.</param>
    /// <param name="configure">The moves that place the zone and set what a press of it does.</param>
    /// <returns>The handle of the zone.</returns>
    public ButtonHandle Zone(string id, Action<ButtonHandle> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ButtonHandle handle = Zone(id);
        configure(handle);
        return handle;
    }

    /// <summary>Creates an image under the current parent.</summary>
    /// <param name="id">The name of the element inside the window.</param>
    /// <returns>The handle of the image.</returns>
    public ImageHandle Image(string id)
    {
        Entity entity = Add(id, Parent);
        _world.Set(entity, new ImageComponent());
        return new ImageHandle(_world, entity);
    }

    /// <summary>Creates an image under the current parent, configured by the caller.</summary>
    /// <param name="id">The name of the element inside the window.</param>
    /// <param name="configure">The moves that write the path of the image and place it.</param>
    /// <returns>The handle of the image.</returns>
    public ImageHandle Image(string id, Action<ImageHandle> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ImageHandle handle = Image(id);
        configure(handle);
        return handle;
    }

    /// <summary>Creates a progress bar under the current parent.</summary>
    /// <param name="id">The name of the element inside the window.</param>
    /// <returns>The handle of the bar.</returns>
    /// <remarks>
    /// A bar of the interface carries a panel of its own, which is the colour of its filled part: the pass of the
    /// interface draws a track of its own and fills it with the colour of the panel, which is what a window writes when
    /// it says what colour a bar is drawn in.
    /// </remarks>
    public ProgressBarHandle ProgressBar(string id)
    {
        Entity entity = Add(id, Parent);
        _world.Set(entity, new ProgressBarComponent { Minimum = 0f, Maximum = 1f, Value = 0f });
        _world.Set(entity, new PanelComponent { Color = new Color(110, 200, 140) });
        return new ProgressBarHandle(_world, entity);
    }

    /// <summary>Creates a progress bar under the current parent, configured by the caller.</summary>
    /// <param name="id">The name of the element inside the window.</param>
    /// <param name="configure">The moves that place the bar and give it its colour.</param>
    /// <returns>The handle of the bar.</returns>
    public ProgressBarHandle ProgressBar(string id, Action<ProgressBarHandle> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ProgressBarHandle handle = ProgressBar(id);
        configure(handle);
        return handle;
    }

    /// <summary>Remembers what a button of the window does when it is pressed.</summary>
    /// <param name="button">The button that answers the press.</param>
    /// <param name="action">The handler the window service calls from the event of the frame.</param>
    internal void RegisterClick(Entity button, Action action) => _clicks[button] = action;

    private Entity Parent => _parents.Peek();

    private void PopParent() => _parents.Pop();

    private Entity AddLabel(string id)
    {
        Entity entity = Add(id, Parent);
        _world.Set(entity, new TextComponent
        {
            Style = new TextStyle
            {
                Color = new Color(225, 225, 225),
                VerticalAlign = TextVerticalAlign.Middle,
            },
        });
        return entity;
    }

    private Entity Add(string id, Entity parent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (_elements.ContainsKey(id))
        {
            throw new InvalidOperationException($"The window already holds an element named '{id}', and a name is unique inside one window.");
        }

        Entity entity = CreateCanvasChild(parent, id);
        _elements[id] = entity;
        return entity;
    }

    private Entity CreateCanvasChild(Entity parent, string id)
    {
        Entity entity = _world.CreateEntity();
        _world.Set(entity, new UiElementComponent { Id = id });
        _world.Set(entity, new RectTransformComponent
        {
            Anchored = true,
            AnchorMin = Vector2.Zero,
            AnchorMax = Vector2.Zero,
            Pivot = Vector2.Zero,
            AnchoredPosition = Vector2.Zero,
            SizeDelta = new Vector2(100f, 30f),
            ZOrder = 0,
            Visible = true,
        });
        _world.Set(entity, new ParentComponent { Parent = _world.Reference(parent) });

        if (_world.Has<ChildrenComponent>(parent))
        {
            ref ChildrenComponent children = ref _world.GetRef<ChildrenComponent>(parent);
            EntityRef[] current = children.Children ?? [];
            EntityRef[] updated = new EntityRef[current.Length + 1];
            current.CopyTo(updated, 0);
            updated[^1] = _world.Reference(entity);
            children.Children = updated;
        }
        else
        {
            _world.Set(parent, new ChildrenComponent { Children = [_world.Reference(entity)] });
        }

        return entity;
    }

    /// <summary>The scope of a panel that holds the elements of a block, which returns the parent of the builder when it ends.</summary>
    public readonly struct PanelScope : IDisposable
    {
        private readonly UiBuilder _builder;

        internal PanelScope(UiBuilder builder) => _builder = builder;

        /// <summary>Takes the panel off the top of the stack, so the elements that follow it are added to its parent.</summary>
        public void Dispose() => _builder.PopParent();
    }
}
