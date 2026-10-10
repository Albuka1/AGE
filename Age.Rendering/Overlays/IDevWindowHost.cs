using Age.Core;

namespace Age.Rendering;

/// <summary>
/// A window of the operating system that a developer window is drawn into: the surface, its events and the pointer, kept apart from
/// <see cref="IWindowService"/> so a second window does not have to be the window of the game.
/// </summary>
/// <remarks>
/// The engine's own window is <see cref="IWindowService"/>, which owns one window and one context and is what the game is drawn
/// into. A developer window is a second one, which is why it is a seam of its own: a backend answers it with a platform window, and a
/// run with no window answers it with a window that is never there, so a game that asks for one is not a game that needs one.
/// </remarks>
public interface IDevWindowHost
{
    /// <summary>Gets a value indicating whether the window exists and is not closing.</summary>
    bool IsOpen { get; }

    /// <summary>Gets the size of the surface of the window, in pixels, which is what its content is laid out in.</summary>
    Vector2 Size { get; }

    /// <summary>Gets the pointer position in the window, in pixels, with the origin at its top-left corner.</summary>
    Vector2 Pointer { get; }

    /// <summary>Gets a value indicating whether the left button of the pointer is held down.</summary>
    bool PointerDown { get; }

    /// <summary>Creates the window the first time it is opened, which is what makes a window that is never opened cost nothing.</summary>
    /// <param name="width">The width of the window, in pixels.</param>
    /// <param name="height">The height of the window, in pixels.</param>
    /// <param name="title">The title of the window, which the window manager draws in its own title bar.</param>
    void Create(int width, int height, string title);

    /// <summary>Reads the events of the operating system that arrived since the last call.</summary>
    /// <param name="clicked">Set to the position a left button was pressed at this frame, or null when none was pressed.</param>
    /// <returns><see langword="true"/> while the window is open, and <see langword="false"/> once it was closed.</returns>
    bool Pump(out Vector2? clicked);

    /// <summary>Begins a frame on this window, which is what makes its context the one that is drawn into.</summary>
    /// <param name="clear">Whether the frame starts by clearing what was drawn before.</param>
    void BeginFrame(bool clear);

    /// <summary>Ends a frame on this window, which is what swaps its buffers and shows what was drawn.</summary>
    void EndFrame();

    /// <summary>Draws a filled rectangle in the pixels of the window, which is what its panels are made of.</summary>
    /// <param name="rect">The rectangle to fill.</param>
    /// <param name="color">The color to fill it with.</param>
    void DrawRectangle(Rect rect, Color color);

    /// <summary>Draws a line of text in the pixels of the window.</summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="position">The top-left corner of the line.</param>
    /// <param name="color">The color of the glyphs.</param>
    void DrawText(string text, Vector2 position, Color color);

    /// <summary>Returns the size a line takes, which is what the label of a tab is measured with.</summary>
    /// <param name="text">The line to measure.</param>
    Vector2 Measure(string text);

    /// <summary>Keeps the draws that follow inside a rectangle of the window, which is what a page that is longer than its body is held in by.</summary>
    /// <param name="rect">The rectangle to keep the draws inside, in the pixels of the window.</param>
    /// <remarks>
    /// A push nests, and what was pushed is undone by <see cref="PopClip"/>. A host that draws without clipping ignores both calls,
    /// so a page that clips is drawn whole rather than refused.
    /// </remarks>
    void PushClip(Rect rect);

    /// <summary>Undoes the most recent <see cref="PushClip"/>.</summary>
    void PopClip();

    /// <summary>Closes the window and releases what it owns.</summary>
    void Dispose();
}
