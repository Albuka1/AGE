using Age.Core;

namespace Age.Rendering;

/// <summary>
/// The pointer of a developer window for one frame: where it is, whether its left button went down this frame, and whether it
/// is held.
/// </summary>
/// <param name="Position">The pointer position, in the pixels of the window, with the origin at its top-left corner.</param>
/// <param name="Pressed">Whether the left button went down during this frame, which is a click a page acts on once.</param>
/// <param name="Down">Whether the left button is held, which is what a drag of a selection or of a scrollbar is read with.</param>
/// <remarks>
/// A page is handed the pointer of the window it is drawn in, which is what lets a page select a line, press a small
/// control of its own, or drag the bar of a list that is longer than the room it was given. The window reads the pointer
/// for its own title bar and tabs first, so a click a page sees is a click that landed inside the body rather than on the
/// frame of the window. What a page acts on once is <see cref="Pressed"/>; what a page follows while a person drags is
/// <see cref="Down"/>, and the two are told apart because a list that folded a row on <see cref="Down"/> would fold it on
/// every frame the pointer spent over it.
/// </remarks>
public readonly record struct WindowPointer(Vector2 Position, bool Pressed, bool Down)
{
    /// <summary>Gets a pointer that is nowhere and is not pressed, which is what a frame with no window hands a page.</summary>
    public static WindowPointer None => default;
}
