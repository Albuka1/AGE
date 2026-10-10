using Age.Core;
using Age.Input;

namespace Age.Rendering;

/// <summary>
/// The developer window of a game: a panel that stands over the frame, with a title bar that is dragged and a row of tabs, one of
/// which is shown at a time.
/// </summary>
/// <remarks>
/// <para>
/// A console is a page of a terminal and reads a line; a developer window is a place to look at the game rather than talk to it. It
/// is a pass of its own, drawn over everything else, and it holds a set of <see cref="IDevWindowTab"/> that a game fills in: the
/// window owns the frame, the title bar and the tabs, and each tab owns the page inside it. The console of the engine is one of
/// those tabs out of the box, so the window is useful the moment it is added.
/// </para>
/// <para>
/// The window is opened and closed with a key of its own, F1 by default, and closed with Escape while it is open. The title bar is
/// dragged with the left button, and the window is kept inside the frame, so it is not lost off an edge. While it is open it keeps
/// the pointer for itself, so a game does not read a click that landed on a tab as one that landed on the world.
/// </para>
/// <para>
/// The window draws with the font of the engine when one is given and with the built-in bitmap font otherwise, the way the other
/// overlays of the engine do.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// DevWindow window = provider.GetRequiredService&lt;DevWindow&gt;();
/// window.Add(new MyTab());
/// renderPipeline.Add(window);
///
/// gameLoop.Run(
///     update: step =&gt; world.Update(step, pipeline),
///     render: time =&gt;
///     {
///         world.UpdateFrame(time, pipeline);
///         window.Update(time);
///         renderPipeline.Render(world, camera);
///     });
/// </code>
/// </example>
public sealed partial class DevWindow : IRenderPass
{
    /// <summary>The panel behind everything the window draws, which the game is visible through.</summary>
    private static readonly Color PanelColour = new(16, 16, 20);

    /// <summary>The strip of the title bar, which is what a person drags the window by.</summary>
    private static readonly Color TitleColour = new(34, 34, 44);

    /// <summary>The line under the title bar and the row of tabs, which separates them from the page.</summary>
    private static readonly Color BorderColour = new(80, 80, 92);

    /// <summary>The colour of the tab that is shown, which is what says which page is on top.</summary>
    private static readonly Color ActiveTabColour = new(58, 58, 76);

    /// <summary>The colour of a tab that is not shown.</summary>
    private static readonly Color InactiveTabColour = new(24, 24, 32);

    /// <summary>The colour of the label of the title bar.</summary>
    private static readonly Color TitleTextColour = new(190, 205, 255);

    /// <summary>The colour of the label of a tab.</summary>
    private static readonly Color TabTextColour = new(215, 215, 225);

    /// <summary>The gap between the edge of the window and what it holds, in pixels.</summary>
    private const float Padding = 8f;

    /// <summary>The height of the title bar, in pixels.</summary>
    private const float TitleHeight = 24f;

    /// <summary>The height of the row of tabs, in pixels.</summary>
    private const float TabHeight = 24f;

    /// <summary>The width of a tab that its label does not make wider, in pixels.</summary>
    private const float TabWidth = 96f;

    /// <summary>The side of the square of a cross that closes a tab or the window, in pixels.</summary>
    private const float CloseSize = 12f;

    /// <summary>The colour of the cross of a tab that is not under the pointer.</summary>
    private static readonly Color CloseColour = new(150, 150, 165);

    /// <summary>The colour of the cross that the pointer is over, which is what makes it read as a button.</summary>
    private static readonly Color CloseHotColour = new(255, 130, 130);

    private readonly IInputService _input;
    private readonly IRenderer _renderer;
    private readonly TextRenderer? _textRenderer;
    private readonly ITextSource? _textSource;
    private readonly List<IDevWindowTab> _tabs = [];

    private Vector2 _position = new(64f, 64f);
    private Vector2 _size = new(560f, 340f);
    private bool _dragging;
    private Vector2 _grab;
    private int _active;

    /// <summary>Initializes the window from the services it reads and draws with.</summary>
    /// <param name="input">The keys that open and close the window and the pointer that drags it.</param>
    /// <param name="renderer">The renderer that the window draws with.</param>
    /// <param name="textRenderer">The renderer of the text of the window, which draws it with the fonts of the engine, or null to draw it with the built-in font.</param>
    /// <param name="textSource">The source of the strings of the window, or null to draw the keys themselves.</param>
    /// <exception cref="ArgumentNullException">The input or the renderer is null.</exception>
    public DevWindow(IInputService input, IRenderer renderer, TextRenderer? textRenderer = null, ITextSource? textSource = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(renderer);

        _input = input;
        _renderer = renderer;
        _textRenderer = textRenderer;
        _textSource = textSource;
    }

    /// <summary>Gets or sets the key that opens and closes the window. The default is F1.</summary>
    /// <remarks>Escape closes the window as well while it is open, so a game that keeps Escape for a menu still has a way out.</remarks>
    public Key OpenKey { get; set; } = Key.F1;

    /// <summary>Gets a value indicating whether the window is open, which is when it takes the pointer for itself.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Gets or sets the top-left corner of the window, in the pixels of the frame. It is kept inside the frame when it is drawn.</summary>
    public Vector2 Position
    {
        get => _position;
        set => _position = value;
    }

    /// <summary>Gets or sets the size of the window, in pixels.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A side of the size is not positive.</exception>
    public Vector2 Size
    {
        get => _size;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.X);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.Y);
            _size = value;
        }
    }

    /// <summary>Gets the tabs of the window, in the order they were added, which is the order their labels are drawn in.</summary>
    public IReadOnlyList<IDevWindowTab> Tabs => _tabs;

    /// <summary>Gets or sets the index of the tab that is shown, which is wrapped into the tabs the window holds.</summary>
    /// <remarks>An index past the last tab starts over at the first, so a key that walks the tabs never lands outside them.</remarks>
    public int ActiveTab
    {
        get => _active;
        set => _active = _tabs.Count == 0 ? 0 : (((value % _tabs.Count) + _tabs.Count) % _tabs.Count);
    }

    /// <summary>Adds a tab to the window, at the end of the row of tabs.</summary>
    /// <param name="tab">The tab to add.</param>
    /// <returns>The window, so that several tabs are added in one line.</returns>
    /// <exception cref="ArgumentNullException">The tab is null.</exception>
    /// <remarks>The first tab that is added becomes the one that is shown, so a window with tabs is never empty.</remarks>
    public DevWindow Add(IDevWindowTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);

        _tabs.Add(tab);

        if (_tabs.Count == 1)
        {
            _active = 0;
        }

        return this;
    }

    /// <summary>Removes a tab from the window, which is what the cross of its label does.</summary>
    /// <param name="tab">The tab to remove.</param>
    /// <returns><see langword="true"/> when the tab was there and has been removed.</returns>
    /// <exception cref="ArgumentNullException">The tab is null.</exception>
    /// <remarks>
    /// The index that was shown follows the tab that was taken: when the tab before it went, the same tab stays on top, and when the
    /// tab itself went, the one that takes its place is shown. The last tab of a window may be removed, which leaves it open with
    /// nothing in it rather than closing it, so a cross never closes a window by surprise.
    /// </remarks>
    public bool Remove(IDevWindowTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);

        int index = _tabs.IndexOf(tab);

        if (index < 0)
        {
            return false;
        }

        _tabs.RemoveAt(index);

        if (_tabs.Count == 0)
        {
            _active = 0;
        }
        else if (index < _active)
        {
            _active--;
        }
        else if (index == _active)
        {
            _active = Math.Min(_active, _tabs.Count - 1);
        }

        return true;
    }

    /// <summary>Opens the window.</summary>
    public void Open() => IsOpen = true;

    /// <summary>Closes the window.</summary>
    public void Close()
    {
        IsOpen = false;
        _dragging = false;
    }

    /// <summary>Opens the window when it is closed and closes it when it is open.</summary>
    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
            return;
        }

        Open();
    }
}

