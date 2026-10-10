using Age.Core;

namespace Age.Rendering;

/// <summary>
/// One page of a developer window: what a tab of the window shows, and the name it shows on its tab.
/// </summary>
/// <remarks>
/// <para>
/// The window owns the frame of the panel, the title bar and the row of tabs, and a page owns everything inside it: a page draws its
/// own lines and reads its own pointer while it is the one that is shown, and it says nothing about where the window stands. That is
/// what lets a game add a page of its own without touching the window, and the window without knowing what a page holds.
/// </para>
/// <para>
/// A page is registered like any other service and handed to the window by <see cref="IDevWindowService.Add"/>, so a page that needs
/// the services of the engine takes them the way the rest of the engine does. The window draws a page through the same
/// <see cref="IRenderer"/> a pass of the game draws through, so a page draws rectangles and text and nothing that belongs to the
/// world.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyPage : IDevWindowTab
/// {
///     public string Title => "my page";
///
///     public void Update(in GameTime frame, Rect body) { }
///
///     public void Render(IRenderer renderer, Rect body) =&gt; renderer.DrawText("hello", body.Position, Color.White);
/// }
/// </code>
/// </example>
public interface IDevWindowTab
{
    /// <summary>Gets the name of the tab, which is what its label says.</summary>
    string Title { get; }

    /// <summary>Gets a value indicating whether the tab may be closed by its cross, which is <see langword="true"/> by default.</summary>
    /// <remarks>
    /// A page that reports what the engine holds rather than a thing a person opened, such as the output of the console, may refuse to
    /// close: a cross that is not drawn is a page that stays.
    /// </remarks>
    bool Closable => true;

    /// <summary>Reads the pointer and the time of one frame while this page is the one that is shown.</summary>
    /// <param name="frame">The time of the frame.</param>
    /// <param name="body">The rectangle inside the window that the page owns, in the pixels of the window.</param>
    /// <param name="pointer">The pointer of the window, which is what a page selects a line or presses a control of its own with.</param>
    /// <remarks>
    /// The window has already claimed the pointer for its title bar and its tabs, so a click a page sees is a click that landed inside
    /// the body. A page that reads nothing does nothing here.
    /// </remarks>
    void Update(in GameTime frame, Rect body, in WindowPointer pointer);

    /// <summary>Draws the body of this tab, which is everything inside the window but the title bar and the row of tabs.</summary>
    /// <param name="renderer">The renderer to draw with. It begins no frame and ends none: the window owns the frame.</param>
    /// <param name="body">The rectangle inside the window that the tab owns, in the pixels of the frame.</param>
    /// <remarks>The draws of a tab are clipped to <paramref name="body"/> by the window, so a tab may draw past its edge and nothing of it lands outside.</remarks>
    void Render(IRenderer renderer, Rect body);
}
