using Age.Core;

namespace Age.Rendering;

/// <summary>Reads the keys and the pointer of the developer window, which is the half of it that is not drawing.</summary>
public sealed partial class DevWindow
{
    /// <summary>Reads the keys and the pointer of one frame, which is what opens the window, drags it and walks its tabs.</summary>
    /// <param name="frame">The time of the frame, which is handed to the tab that is shown.</param>
    /// <remarks>
    /// Call it once per frame from the render callback of the loop, before the passes are rendered. The tab that is shown is read
    /// after the window has taken what is its own, so a tab that uses the pointer does not fight the title bar or the tabs for it.
    /// </remarks>
    public void Update(in GameTime frame)
    {
        if (_input.IsKeyPressed(OpenKey))
        {
            Toggle();
        }

        if (!IsOpen)
        {
            return;
        }

        if (_input.IsKeyPressed(Key.Escape))
        {
            Close();
            return;
        }

        Rect window = WindowRect(_renderer.ViewportSize);

        Drag(window);

        // The crosses are read before the tabs and the page, because a click on one is a click that closes and not one that picks a
        // tab or lands on the page below it.
        if (Clicked(window))
        {
            return;
        }

        PickTab(window);
        WalkTabs();

        if (_tabs.Count > 0)
        {
            _tabs[_active].Update(frame, BodyRect(window));
        }
    }

    /// <summary>Closes a tab or the window when the cross of one was clicked, which is what the crosses are for.</summary>
    /// <param name="window">The rectangle of the window as it is drawn this frame.</param>
    /// <returns><see langword="true"/> when a cross took the click, so nothing below the title bar reads it.</returns>
    /// <remarks>
    /// The cross of the title bar closes the window itself, and the cross of a tab closes that tab. A tab that is not closable has no
    /// cross and is not asked, so a page that reports what the engine holds stays where it is.
    /// </remarks>
    private bool Clicked(Rect window)
    {
        if (!_input.IsMouseButtonPressed(MouseButton.Left))
        {
            return false;
        }

        Vector2 pointer = _input.MousePosition;

        // The cross of the window stands at the right end of the title bar, which is where a person reaches for it.
        if (Contains(CloseRect(window.X + window.Width - TitleHeight, window.Y), pointer))
        {
            Close();
            return true;
        }

        float x = window.X;

        for (var index = 0; index < _tabs.Count; index++)
        {
            IDevWindowTab tab = _tabs[index];
            float width = TabWidthOf(tab);

            if (tab.Closable && Contains(CloseRect(x + width - TabHeight, window.Y + TitleHeight), pointer))
            {
                // Removing keeps the page that was on top on top when a tab before it went, which is what Remove answers.
                Remove(tab);
                return true;
            }

            x += width;
        }

        return false;
    }

    /// <summary>Returns the square of the cross that stands at the right end of a bar of the given width.</summary>
    /// <param name="x">The left edge of the bar the cross stands in.</param>
    /// <param name="y">The top edge of the bar the cross stands in.</param>
    private static Rect CloseRect(float x, float y) => new(new Vector2(x + ((TitleHeight - CloseSize) / 2f), y + ((TitleHeight - CloseSize) / 2f)), new Vector2(CloseSize, CloseSize));

    /// <summary>Drags the window by its title bar with the left button, keeping it inside the frame.</summary>
    /// <param name="window">The rectangle of the window as it is drawn this frame.</param>
    /// <remarks>
    /// The window follows the pointer by the same offset it was grabbed at, so it does not jump under the cursor the moment it is
    /// picked up, and it is kept inside the frame rather than dragged off an edge where it could not be reached again.
    /// </remarks>
    private void Drag(Rect window)
    {
        var title = new Rect(window.Position, new Vector2(window.Width, TitleHeight));
        Vector2 pointer = _input.MousePosition;

        if (_input.IsMouseButtonPressed(MouseButton.Left) && Contains(title, pointer))
        {
            _dragging = true;
            _grab = pointer - _position;
        }

        if (!_input.IsMouseButtonDown(MouseButton.Left))
        {
            _dragging = false;
        }

        if (_dragging)
        {
            _position = Clamp(pointer - _grab, _size, _renderer.ViewportSize);
        }
    }

    /// <summary>Shows the tab whose label a click landed on, which is what a row of tabs is for.</summary>
    /// <param name="window">The rectangle of the window as it is drawn this frame.</param>
    private void PickTab(Rect window)
    {
        if (!_input.IsMouseButtonPressed(MouseButton.Left))
        {
            return;
        }

        Vector2 pointer = _input.MousePosition;
        float x = window.X;

        for (var index = 0; index < _tabs.Count; index++)
        {
            float width = TabWidthOf(_tabs[index]);

            if (pointer.X >= x && pointer.X < x + width && pointer.Y >= window.Y + TitleHeight && pointer.Y < window.Y + TitleHeight + TabHeight)
            {
                _active = index;
                return;
            }

            x += width;
        }
    }

    /// <summary>Walks the tabs with the tab key, which is what a person reaches for when the pointer is not on the window.</summary>
    /// <remarks>
    /// The tab key walks the tabs forward and, with Shift held, back, which matches the way a control of a desktop walks a row of
    /// tabs rather than the way a text field takes a tab character.
    /// </remarks>
    private void WalkTabs()
    {
        if (!_input.IsKeyPressed(Key.Tab) || _tabs.Count < 2)
        {
            return;
        }

        bool back = _input.IsKeyDown(Key.ShiftLeft) || _input.IsKeyDown(Key.ShiftRight);
        ActiveTab = back ? _active - 1 : _active + 1;
    }

    /// <summary>Returns the rectangle of the window as it is drawn, kept inside a frame of the given size.</summary>
    /// <param name="viewport">The size of the frame.</param>
    private Rect WindowRect(Vector2 viewport) => new(Clamp(_position, _size, viewport), _size);

    /// <summary>Returns the rectangle inside the window that the tab that is shown owns.</summary>
    /// <param name="window">The rectangle of the window as it is drawn.</param>
    private static Rect BodyRect(Rect window) => new(
        new Vector2(window.X + Padding, window.Y + TitleHeight + TabHeight + Padding),
        new Vector2(Math.Max(0f, window.Width - (2f * Padding)), Math.Max(0f, window.Height - TitleHeight - TabHeight - (2f * Padding))));

    /// <summary>Returns a position of the window kept inside a frame, so no part of it is off an edge.</summary>
    /// <param name="position">The position asked for.</param>
    /// <param name="size">The size of the window.</param>
    /// <param name="viewport">The size of the frame.</param>
    private static Vector2 Clamp(Vector2 position, Vector2 size, Vector2 viewport) => new(
        Math.Clamp(position.X, 0f, Math.Max(0f, viewport.X - size.X)),
        Math.Clamp(position.Y, 0f, Math.Max(0f, viewport.Y - size.Y)));

    /// <summary>Returns a value indicating whether a point is inside a rectangle, which is what a click is tested with.</summary>
    /// <param name="rect">The rectangle to test.</param>
    /// <param name="point">The point to test.</param>
    private static bool Contains(Rect rect, Vector2 point) =>
        point.X >= rect.X && point.X < rect.X + rect.Width && point.Y >= rect.Y && point.Y < rect.Y + rect.Height;
}
