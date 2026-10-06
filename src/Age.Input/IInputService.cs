using Age.Core;

namespace Age.Input;

/// <summary>
/// Provides keyboard and mouse state for the current frame.
/// </summary>
/// <remarks>
/// The service describes one frame. The "down" queries report what is held right now, while the "pressed" queries report
/// a transition that happened since the previous frame, so a key that stays held is pressed once. The pointer position is
/// in screen pixels with the origin at the top-left corner of the window.
/// <para>
/// One implementation deliberately deviates: <see cref="NullInputService"/> simulates a single held flag, so its left
/// button reports <see cref="IsMouseButtonPressed"/> on every frame that flag is set instead of only on the transition.
/// Implementations that drive a real game are expected to follow the transition rule above.
/// </para>
/// </remarks>
public interface IInputService
{
    /// <summary>Determines whether the key is currently held down.</summary>
    /// <param name="key">The key to inspect.</param>
    /// <returns><see langword="true"/> while the key is held, including the frame it was pressed on.</returns>
    bool IsKeyDown(Key key);

    /// <summary>Determines whether the key transitioned to the down state during the current frame.</summary>
    /// <param name="key">The key to inspect.</param>
    /// <returns><see langword="true"/> only on the frame the key went down.</returns>
    bool IsKeyPressed(Key key);

    /// <summary>Gets the pointer position, in screen pixels, with the origin at the top-left corner of the window.</summary>
    Vector2 MousePosition { get; }

    /// <summary>Determines whether the mouse button is currently held down.</summary>
    /// <param name="button">The button to inspect.</param>
    /// <returns><see langword="true"/> while the button is held, including the frame it was pressed on.</returns>
    bool IsMouseButtonDown(MouseButton button);

    /// <summary>Determines whether the mouse button transitioned to the down state during the current frame.</summary>
    /// <param name="button">The button to inspect.</param>
    /// <returns><see langword="true"/> only on the frame the button went down.</returns>
    bool IsMouseButtonPressed(MouseButton button);
}
