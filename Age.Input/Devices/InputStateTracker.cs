using Age.Core;

namespace Age.Input;

/// <summary>
/// Tracks keyboard and mouse state from raw events and answers the queries of <see cref="IInputService"/> for the frame
/// between two <see cref="BeginFrame"/> calls.
/// </summary>
/// <remarks>
/// This is the backend independent half of an input implementation: an adapter reports what the platform sees, either as
/// events through <see cref="KeyDown"/> and <see cref="KeyUp"/> or as one snapshot per frame through
/// <see cref="SetKey"/>, and the game queries the result. A key that is already down when reporting starts is not
/// reported as pressed, because that transition happened before the tracker was watching.
/// </remarks>
public sealed class InputStateTracker : IInputService
{
    private readonly HashSet<Key> _down = new();
    private readonly HashSet<Key> _pressed = new();
    private readonly HashSet<MouseButton> _mouseDown = new();
    private readonly HashSet<MouseButton> _mousePressed = new();
    private Vector2 _mousePosition;

    /// <inheritdoc />
    public Vector2 MousePosition => _mousePosition;

    /// <inheritdoc />
    public void BeginFrame()
    {
        _pressed.Clear();
        _mousePressed.Clear();
    }

    /// <summary>Records that the key went down and reports it as pressed for the current frame.</summary>
    /// <param name="key">The key that went down.</param>
    public void KeyDown(Key key)
    {
        if (_down.Add(key))
        {
            _pressed.Add(key);
        }
    }

    /// <summary>Records that the key was released.</summary>
    /// <param name="key">The key that was released.</param>
    public void KeyUp(Key key) => _down.Remove(key);

    /// <summary>Records the current state of a key, which is how a polling backend reports it. A key that is already down is not reported as pressed again.</summary>
    /// <param name="key">The key to record.</param>
    /// <param name="isDown">Whether the key is held down.</param>
    public void SetKey(Key key, bool isDown)
    {
        if (isDown)
        {
            KeyDown(key);
            return;
        }

        KeyUp(key);
    }

    /// <summary>Records that the pointer moved.</summary>
    /// <param name="position">The new pointer position, in screen pixels.</param>
    public void MouseMove(Vector2 position) => _mousePosition = position;

    /// <summary>Records that the button went down and reports it as pressed for the current frame.</summary>
    /// <param name="button">The button that went down.</param>
    public void MouseDown(MouseButton button)
    {
        if (_mouseDown.Add(button))
        {
            _mousePressed.Add(button);
        }
    }

    /// <summary>Records that the button was released.</summary>
    /// <param name="button">The button that was released.</param>
    public void MouseUp(MouseButton button) => _mouseDown.Remove(button);

    /// <summary>Records the current state of a mouse button, which is how a polling backend reports it. A button that is already down is not reported as pressed again.</summary>
    /// <param name="button">The button to record.</param>
    /// <param name="isDown">Whether the button is held down.</param>
    public void SetMouseButton(MouseButton button, bool isDown)
    {
        if (isDown)
        {
            MouseDown(button);
            return;
        }

        MouseUp(button);
    }

    /// <summary>Forgets every key and button, for example when the window loses focus and the release events never arrive.</summary>
    public void ReleaseAll()
    {
        _down.Clear();
        _pressed.Clear();
        _mouseDown.Clear();
        _mousePressed.Clear();
    }

    /// <inheritdoc />
    public bool IsKeyDown(Key key) => _down.Contains(key);

    /// <inheritdoc />
    public bool IsKeyPressed(Key key) => _pressed.Contains(key);

    /// <inheritdoc />
    public bool IsMouseButtonDown(MouseButton button) => _mouseDown.Contains(button);

    /// <inheritdoc />
    public bool IsMouseButtonPressed(MouseButton button) => _mousePressed.Contains(button);
}
