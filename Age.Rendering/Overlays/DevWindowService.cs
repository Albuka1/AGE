using Age.Core;

namespace Age.Rendering;

/// <summary>
/// The developer window of a game: a window of the operating system that stands beside the game and holds its pages as tabs, drawn
/// over an <see cref="IDevWindowHost"/>.
/// </summary>
/// <remarks>
/// <para>
/// The page of a tab is drawn by the tab itself and the frame of the window — the title bar, the row of tabs, the crosses and the
/// clip of the body — is drawn here, which is what the window owns and what a page leaves to it.
/// was written for one works in the other, because the seam is <see cref="IDevWindowTab"/> either way.
/// </para>
/// <para>
/// The window is created the first time it is opened, so a game that never opens it never pays for it, and its size is kept while it
/// is closed, so what a person dragged to a size is the size it comes back at. A host that answers with no window, which is what a
/// headless run registers, leaves the service with its pages and nothing drawn.
/// </para>
/// </remarks>
public sealed class DevWindowService : IDevWindowService
{
    private static readonly Color PanelColour = new(16, 16, 20);
    private static readonly Color TitleColour = new(34, 34, 44);
    private static readonly Color BorderColour = new(80, 80, 92);
    private static readonly Color ActiveTabColour = new(58, 58, 76);
    private static readonly Color InactiveTabColour = new(24, 24, 32);
    private static readonly Color TabTextColour = new(215, 215, 225);
    private static readonly Color CloseColour = new(150, 150, 165);
    private static readonly Color CloseHotColour = new(255, 130, 130);

    private const float Padding = 8f;
    private const float TitleHeight = 26f;
    private const float TabHeight = 26f;
    private const float TabWidth = 96f;
    private const float CloseSize = 12f;

    private readonly IDevWindowHost _host;
    private readonly List<IDevWindowTab> _tabs = [];

    private int _width = 640;
    private int _height = 400;
    private bool _open;
    private int _active;

    /// <summary>Initializes the window over the host it draws itself into.</summary>
    /// <param name="host">The window of the operating system, or a host that opens nothing in a run with no window.</param>
    /// <exception cref="ArgumentNullException">The host is null.</exception>
    public DevWindowService(IDevWindowHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        _host = host;
    }

    /// <inheritdoc />
    public bool IsOpen => _open && _host.IsOpen;

    /// <inheritdoc />
    public IReadOnlyList<IDevWindowTab> Tabs => _tabs;

    /// <inheritdoc />
    public int ActiveTab
    {
        get => _active;
        set => _active = _tabs.Count == 0 ? 0 : (((value % _tabs.Count) + _tabs.Count) % _tabs.Count);
    }

    /// <summary>Gets or sets the size of the window, in pixels, which is what it is created at and kept at while it is closed.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A side of the size is not positive.</exception>
    public Vector2 Size
    {
        get => new(_width, _height);
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.X);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.Y);
            _width = (int)value.X;
            _height = (int)value.Y;
        }
    }

    /// <inheritdoc />
    public IDevWindowService Add(IDevWindowTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);

        _tabs.Add(tab);

        if (_tabs.Count == 1)
        {
            _active = 0;
        }

        return this;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void Open()
    {
        if (!_open)
        {
            _host.Create(_width, _height, ResolveTitle());
            _open = true;
        }
    }

    /// <inheritdoc />
    public void Close()
    {
        // The window of the operating system is closed rather than kept hidden, so what it owns — a context and its surface — goes
        // with it, and a game that opens it again creates a window of its own rather than holding one for the whole run.
        _host.Dispose();
        _open = false;
    }

    /// <inheritdoc />
    public void Toggle()
    {
        if (_open)
        {
            Close();
            return;
        }

        Open();
    }

    /// <inheritdoc />
    public void Pump(in GameTime frame)
    {
        if (!_open)
        {
            return;
        }

        // The window manager closed the window, which is what the cross of its own title bar does: the window is disposed here
        // rather than kept, so the next `devwindow` opens a window of its own rather than finding one that is on its way out.
        if (!_host.IsOpen)
        {
            Close();
            return;
        }

        _host.BeginFrame(true);

        // The frame is drawn before the events are read, because what the pointer is over is what tells a cross that it is hot, and
        // the pointer of the last frame is the best answer this one has: a frame that read before it drew would colour a cross by
        // where the pointer was two frames ago.
        Draw();

        if (!_host.Pump(out Vector2? clicked))
        {
            _host.EndFrame();
            Close();
            return;
        }

        // A click on the frame of the window is the window's own — a cross or a tab — and a click inside the body is the page's. The
        // window reads its own first, so a click that closed a tab never reaches the page below it.
        bool insideBody = clicked is Vector2 where && Body(_host.Size) is Rect body && Contains(body, where);

        if (clicked is Vector2 point && !insideBody)
        {
            Clicked(point);
        }

        if (_tabs.Count > 0)
        {
            var pointer = new WindowPointer(_host.Pointer, insideBody, _host.PointerDown);
            _tabs[_active].Update(frame, Body(_host.Size), pointer);
        }

        _host.EndFrame();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_open)
        {
            Close();
        }
    }

    /// <summary>Draws the frame of the window and the page of the tab that is shown.</summary>
    private void Draw()
    {
        Vector2 size = _host.Size;

        _host.DrawRectangle(new Rect(Vector2.Zero, size), PanelColour);
        _host.DrawRectangle(new Rect(Vector2.Zero, new Vector2(size.X, TitleHeight)), TitleColour);
        _host.DrawRectangle(new Rect(new Vector2(0f, TitleHeight), new Vector2(size.X, 1f)), BorderColour);

        float x = 0f;

        for (var index = 0; index < _tabs.Count; index++)
        {
            IDevWindowTab tab = _tabs[index];
            float width = TabWidthOf(tab);

            _host.DrawRectangle(new Rect(new Vector2(x, TitleHeight), new Vector2(width, TabHeight)), index == _active ? ActiveTabColour : InactiveTabColour);
            _host.DrawText(tab.Title, new Vector2(x + Padding, TitleHeight + 6f), TabTextColour);

            if (tab.Closable)
            {
                DrawCross(CloseRect(x + width - TabHeight, TitleHeight));
            }

            x += width;
        }

        _host.DrawRectangle(new Rect(new Vector2(0f, TitleHeight + TabHeight), new Vector2(size.X, 1f)), BorderColour);

        if (_tabs.Count > 0)
        {
            _tabs[_active].Render(new HostRenderer(_host), Body(size));
        }
    }

    /// <summary>Handles a click on the window, which is a cross that closes a page or a tab that shows one.</summary>
    /// <param name="point">The point of the click, in the pixels of the window.</param>
    /// <remarks>
    /// The window itself is closed by the window manager rather than by a cross of its own, so the only crosses it draws are the ones
    /// of the pages that may be closed.
    /// </remarks>
    private void Clicked(Vector2 point)
    {
        float x = 0f;

        for (var index = 0; index < _tabs.Count; index++)
        {
            IDevWindowTab tab = _tabs[index];
            float width = TabWidthOf(tab);

            if (tab.Closable && Contains(CloseRect(x + width - TabHeight, TitleHeight), point))
            {
                Remove(tab);
                return;
            }

            if (point.X >= x && point.X < x + width && point.Y >= TitleHeight && point.Y < TitleHeight + TabHeight)
            {
                _active = index;
                return;
            }

            x += width;
        }
    }

    /// <summary>Draws the cross of a bar, which is what closes a tab or the window.</summary>
    /// <param name="rect">The square the cross stands in.</param>
    private void DrawCross(Rect rect)
    {
        Color colour = Contains(rect, _host.Pointer) ? CloseHotColour : CloseColour;
        const float Thickness = 2f;
        float side = rect.Width;
        int steps = (int)(side / Thickness);

        for (var step = 0; step <= steps; step++)
        {
            float offset = step * (side - Thickness) / steps;

            _host.DrawRectangle(new Rect(new Vector2(rect.X + offset, rect.Y + offset), new Vector2(Thickness, Thickness)), colour);
            _host.DrawRectangle(new Rect(new Vector2(rect.X + side - Thickness - offset, rect.Y + offset), new Vector2(Thickness, Thickness)), colour);
        }
    }

    /// <summary>Returns the rectangle inside the window that the page of a tab owns.</summary>
    private static Rect Body(Vector2 size) => new(
        new Vector2(Padding, TitleHeight + TabHeight + Padding),
        new Vector2(Math.Max(0f, size.X - (2f * Padding)), Math.Max(0f, size.Y - TitleHeight - TabHeight - (2f * Padding))));

    /// <summary>Returns the width of a tab, which its label and its cross make wider than the least a tab is drawn at.</summary>
    private float TabWidthOf(IDevWindowTab tab) => Math.Max(TabWidth, _host.Measure(tab.Title).X + (2f * Padding) + (tab.Closable ? TabHeight : 0f));

    /// <summary>Returns the square of the cross that stands at the right end of a bar of the given width.</summary>
    private static Rect CloseRect(float x, float y) => new(new Vector2(x + ((TitleHeight - CloseSize) / 2f), y + ((TitleHeight - CloseSize) / 2f)), new Vector2(CloseSize, CloseSize));

    /// <summary>Returns a value indicating whether a point is inside a rectangle, which is what a click is tested with.</summary>
    private static bool Contains(Rect rect, Vector2 point) =>
        point.X >= rect.X && point.X < rect.X + rect.Width && point.Y >= rect.Y && point.Y < rect.Y + rect.Height;

    /// <summary>Returns the title that the window manager writes on the window.</summary>
    private static string ResolveTitle() => "devwindow";
}

