using Age.Core;

namespace Age.Input;

/// <summary>
/// Provides keyboard and mouse state for the current frame.
/// </summary>
public interface IInputService
{
    /// <summary>Determines whether the key is currently held down.</summary>
    bool IsKeyDown(Key key);

    /// <summary>Determines whether the key transitioned to the down state during the current frame.</summary>
    bool IsKeyPressed(Key key);

    /// <summary>Gets the pointer position, in screen coordinates.</summary>
    Vector2 MousePosition { get; }

    /// <summary>Determines whether the mouse button is currently held down.</summary>
    bool IsMouseButtonDown(MouseButton button);

    /// <summary>Determines whether the mouse button transitioned to the down state during the current frame.</summary>
    bool IsMouseButtonPressed(MouseButton button);
}
