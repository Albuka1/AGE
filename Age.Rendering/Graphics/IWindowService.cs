using Silk.NET.Windowing;

namespace Age.Rendering;

/// <summary>
/// Owns the application window: it creates it, sizes it, and holds the state of the display that a game sets through it.
/// </summary>
/// <remarks>
/// The window is one object for the life of the run, so a game changes how it is shown rather than creating another one: the
/// title, the fullscreen state, the vertical sync and the frame rate limit are properties of this service, and every one of
/// them reaches the device at the moment it is written. The refresh rate of the monitor is read here as well, because it is
/// what a frame rate limit falls back to when vertical sync is on and no limit was asked for.
/// </remarks>
public interface IWindowService
{
    /// <summary>Gets the underlying window.</summary>
    IWindow Window { get; }

    /// <summary>Creates the window. It must be called before the game loop runs.</summary>
    /// <remarks>The window opens with the icon of the engine. Replace it with <see cref="SetIcon"/>.</remarks>
    void Create(int width, int height, string title);

    /// <summary>Replaces the window icon with the given image.</summary>
    /// <param name="pixels">The pixels, four RGBA bytes each, in row-major order from the top-left corner.</param>
    /// <param name="width">The width of the image, in pixels.</param>
    /// <param name="height">The height of the image, in pixels.</param>
    /// <remarks>
    /// The image becomes the icon in the title bar and, on Windows, in the taskbar, so a square image of 32 or 64
    /// pixels per side suits it. Decode a file into pixels with <see cref="T:Age.Assets.IImageLoader"/> first: this call
    /// takes pixels, not a path, which keeps it free of any file format.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Width or height is zero or negative.</exception>
    /// <exception cref="ArgumentException">The buffer does not hold four bytes for every pixel, or the image is too large to be an icon.</exception>
    /// <exception cref="InvalidOperationException">The window has not been created.</exception>
    void SetIcon(ReadOnlySpan<byte> pixels, int width, int height);

    /// <summary>Closes the window.</summary>
    void Close();

    /// <summary>Gets or sets the title that the window manager writes on the window.</summary>
    /// <remarks>
    /// A game sets this when the state of a run changes in a way that belongs in a title bar — the level that is played, the
    /// profile that is loaded — rather than once at startup. The value reaches the window manager on the frame it is written.
    /// </remarks>
    string Title { get; set; }

    /// <summary>Gets or sets a value indicating whether the window fills the screen.</summary>
    /// <remarks>
    /// The state is written to the window on the frame it changes rather than on every frame, so the switch between a window
    /// and a full screen costs nothing while nothing changes. The value the service answers with is the one that was asked for,
    /// which is what a settings file reads back whether or not the window manager honoured it.
    /// </remarks>
    bool Fullscreen { get; set; }

    /// <summary>Gets or sets a value indicating whether the device waits for the vertical blank between two frames.</summary>
    /// <remarks>
    /// Vertical sync removes tearing and caps the frame rate to the refresh rate of the monitor. It costs a frame of latency, so a
    /// game that measures its own frame time turns it off and limits the frame rate itself with <see cref="FpsLimit"/> instead.
    /// </remarks>
    bool VSync { get; set; }

    /// <summary>Gets or sets the frame rate limit, where zero is unlimited.</summary>
    /// <remarks>
    /// A limit of zero is a loop that limits nothing of its own: when vertical sync is on the loop falls back to
    /// <see cref="RefreshRate"/>, and when the sync is off the loop does not limit at all and runs as fast as the device
    /// answers. A positive value is what the loop keeps the pace of the frames to, with the sync on or off. So a game that
    /// limits the frame rate itself writes a positive value here, and a game that is paced by the vertical sync of the device
    /// leaves this at zero and turns the sync on; a game that writes zero and turns the sync off has a loop that runs as fast
    /// as the platform allows.
    /// </remarks>
    int FpsLimit { get; set; }

    /// <summary>Gets the refresh rate of the monitor the window is on, in hertz, or zero when it cannot be read.</summary>
    /// <remarks>
    /// The rate is asked of the window manager rather than remembered, because a window that was moved to another display has
    /// another one. A window that is not on a monitor yet — because it was created but not shown — answers zero, and so does a
    /// platform that does not report a rate: a loop with vertical sync on is then paced by the device alone, and one with the
    /// sync off and no <see cref="FpsLimit"/> written runs as fast as the platform allows.
    /// </remarks>
    int RefreshRate { get; }

    /// <summary>Writes the state of this service to the window.</summary>
    /// <remarks>
    /// The setters of the properties already reach the window, so this call is what a game makes when it wrote several of them
    /// and would rather apply them in one place than one at a time. It is safe to call on every frame: a value that did not
    /// change is written nowhere.
    /// </remarks>
    void Apply();
}
