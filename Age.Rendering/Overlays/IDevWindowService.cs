using Age.Core;

namespace Age.Rendering;

/// <summary>
/// The developer window of a game as a window of the operating system rather than a panel inside the frame: a window that stands
/// beside the game, is moved and closed by the window manager, and holds the pages of <see cref="IDevWindowTab"/> as tabs.
/// </summary>
/// <remarks>
/// <para>
/// A window of its own is what a developer wants when the game is what is being looked at: a panel inside the frame covers the very
/// thing it reports and is drawn at the resolution of the game, while a window of the operating system stands beside it, is moved to
/// another display and is filled with the pixels of that display. This is the seam that makes one: the game pumps it once per frame,
/// and the service owns the window, its context and its renderer.
/// </para>
/// <para>
/// The window takes the pages of <see cref="IDevWindowTab"/>, which is the seam a page of the window is written against, so a page
/// that was written for one works in the other. A game that registers a service of its own in place of <see cref="DevWindowService"/>
/// answers this interface, which is what a headless run does with a service that opens nothing.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// IDevWindowService dev = provider.GetRequiredService&lt;IDevWindowService&gt;();
/// dev.Add(new MyTab());
///
/// gameLoop.Run(
///     update: step =&gt; world.Update(step, pipeline),
///     render: time =&gt;
///     {
///         world.UpdateFrame(time, pipeline);
///         dev.Pump(time);
///         renderPipeline.Render(world, camera);
///     });
/// </code>
/// </example>
public interface IDevWindowService
{
    /// <summary>Gets a value indicating whether the window of the operating system is open.</summary>
    bool IsOpen { get; }

    /// <summary>Gets the pages of the window, in the order they were added, which is the order their tabs are drawn in.</summary>
    IReadOnlyList<IDevWindowTab> Tabs { get; }

    /// <summary>Gets or sets the index of the page that is shown, which is wrapped into the pages the window holds.</summary>
    int ActiveTab { get; set; }

    /// <summary>Adds a page to the window, at the end of its row of tabs.</summary>
    /// <param name="tab">The page to add.</param>
    /// <returns>The service, so that several pages are added in one line.</returns>
    /// <exception cref="ArgumentNullException">The page is null.</exception>
    IDevWindowService Add(IDevWindowTab tab);

    /// <summary>Removes a page from the window, which is what the cross of its tab does.</summary>
    /// <param name="tab">The page to remove.</param>
    /// <returns><see langword="true"/> when the page was there and has been removed.</returns>
    /// <exception cref="ArgumentNullException">The page is null.</exception>
    bool Remove(IDevWindowTab tab);

    /// <summary>Opens the window of the operating system, creating it the first time it is asked for.</summary>
    void Open();

    /// <summary>Closes the window of the operating system, keeping its pages and what they hold.</summary>
    void Close();

    /// <summary>Opens the window when it is closed and closes it when it is open.</summary>
    void Toggle();

    /// <summary>Pumps the window for one frame: it reads the events of the operating system and draws the page that is shown.</summary>
    /// <param name="frame">The time of the frame, which is handed to the page that is shown.</param>
    /// <remarks>
    /// Call it once per frame from the render callback of the loop. A window that is closed answers at once, so a game that pumps it
    /// every frame pays nothing while it is not there.
    /// </remarks>
    void Pump(in GameTime frame);

    /// <summary>Closes the window of the operating system and releases it, which is what the shutdown of a game calls.</summary>
    void Dispose();
}
