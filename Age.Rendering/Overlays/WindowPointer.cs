using Age.Core;

namespace Age.Rendering;

/// <summary>
/// The pointer of a developer window for one frame: where it is and whether its left button went down this frame.
/// </summary>
/// <param name="Position">The pointer position, in the pixels of the window, with the origin at its top-left corner.</param>
/// <param name="Pressed">Whether the left button went down during this frame, which is a click a page acts on once.</param>
/// <param name="Down">Whether the left button is held, which is what a drag of a selection is read with.</param>
/// <remarks>
/// A page is handed the pointer of the window it is drawn in, which is what lets a page select a line or press a small control of its
/// own. The window reads the pointer for its own title bar and tabs first, so a click a page sees is a click that landed inside the
/// body rather than on the frame of the window.
/// </remarks>
public readonly record struct WindowPointer(Vector2 Position, bool Pressed, bool Down)
{
    /// <summary>Gets a pointer that is nowhere and is not pressed, which is what a frame with no window hands a page.</summary>
    public static WindowPointer None => default;
}
