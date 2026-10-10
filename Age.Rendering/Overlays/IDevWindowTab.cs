using Age.Core;

namespace Age.Rendering;

/// <summary>
/// One page of a <see cref="DevWindow"/>: what a tab of the window shows, and the name it shows on its tab.
/// </summary>
/// <remarks>
/// <para>
/// The window owns the frame of the panel, the title bar and the row of tabs, and a tab owns everything inside the page: a tab draws
/// its own lines and reads its own keys while it is the one that is shown, and it says nothing about where the window stands. That
/// is what lets a game add a page of its own without touching the window, and the window without knowing what a page holds.
/// </para>
/// <para>
/// A tab is registered like any other service and handed to the window by <see cref="DevWindow.Add"/>, so a page that needs the
/// services of the engine takes them the way the rest of the engine does. The window draws a tab with the built-in font unless the
/// tab draws its own text, which is why the seam is drawing rather than a list of strings: a page of a tree, a texture or a plot
/// needs more than lines.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyTab : IDevWindowTab
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

    /// <summary>Reads the keys and the pointer of one frame while this tab is the one that is shown.</summary>
    /// <param name="frame">The time of the frame.</param>
    /// <param name="body">The rectangle inside the window that the tab owns, in the pixels of the frame.</param>
    /// <remarks>
    /// The window has already claimed the pointer for its title bar and its tabs, so a tab that uses the pointer does not fight the
    /// window for it. A tab that reads nothing does nothing here.
    /// </remarks>
    void Update(in GameTime frame, Rect body);

    /// <summary>Draws the body of this tab, which is everything inside the window but the title bar and the row of tabs.</summary>
    /// <param name="renderer">The renderer to draw with. It begins no frame and ends none: the window owns the frame.</param>
    /// <param name="body">The rectangle inside the window that the tab owns, in the pixels of the frame.</param>
    /// <remarks>The draws of a tab are clipped to <paramref name="body"/> by the window, so a tab may draw past its edge and nothing of it lands outside.</remarks>
    void Render(IRenderer renderer, Rect body);
}
