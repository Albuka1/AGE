using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Registers the display settings of a game as console variables and keeps them in step with the window.
/// </summary>
/// <remarks>
/// <para>
/// A game does not have to write the settings of its own window: the three that every game wants — fullscreen, vertical sync and
/// a frame rate limit — are registered here under the names a person expects, and the value of each of them reaches the window on
/// the frame it changes. The settings file of a game is written and read through <see cref="CVarService"/> like every other
/// setting, so this is what a game calls once with the window and the settings of its run.
/// </para>
/// <para>
/// The names are stable, because they are what a person types in the console: <c>fullscreen</c> takes a boolean, <c>vsync</c> takes
/// a boolean, and <c>fps</c> takes a whole number of frames per second, where zero is unlimited. The three are registered with the
/// values the window already has, so a game that set them before the call keeps what it set.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// IWindowService window = provider.GetRequiredService&lt;IWindowService&gt;();
/// CVarService cvars = provider.GetRequiredService&lt;CVarService&gt;();
///
/// WindowCVars.Register(cvars, window);
///
/// // And once per frame, or when the settings file is written:
/// WindowCVars.Pull(cvars, window);
/// </code>
/// </example>
public static class WindowCVars
{
    /// <summary>The name of the setting that fills the screen, which takes a boolean.</summary>
    public const string Fullscreen = "fullscreen";

    /// <summary>The name of the setting that waits for the vertical blank, which takes a boolean.</summary>
    public const string VSync = "vsync";

    /// <summary>The name of the setting that limits the frame rate, which takes a whole number of frames per second and zero for none.</summary>
    public const string Fps = "fps";

    /// <summary>Registers the display settings with the values the window already has.</summary>
    /// <param name="cvars">The settings of the run.</param>
    /// <param name="window">The window that the settings describe.</param>
    /// <exception cref="ArgumentNullException">The settings or the window is null.</exception>
    public static void Register(CVarService cvars, IWindowService window)
    {
        ArgumentNullException.ThrowIfNull(cvars);
        ArgumentNullException.ThrowIfNull(window);

        cvars.Register(Fullscreen, window.Fullscreen, "Whether the window fills the screen. Usage: cvars set fullscreen true.");
        cvars.Register(VSync, window.VSync, "Vertical sync: removes tearing, caps the frame rate to the monitor refresh. Usage: cvars set vsync true.");
        cvars.Register(Fps, window.FpsLimit, "Frame rate limit. 0 is unlimited. Usage: cvars set fps 144.");
    }

    /// <summary>Writes the current value of every display setting to the window.</summary>
    /// <param name="cvars">The settings of the run.</param>
    /// <param name="window">The window that the settings describe.</param>
    /// <exception cref="ArgumentNullException">The settings or the window is null.</exception>
    /// <remarks>
    /// The window writes nothing that did not change, so this is safe to call once per frame. A game that would rather not pays
    /// the cost of one property read per setting and calls it after the settings were read or written, which is where the values
    /// of the window can differ from the values of the settings.
    /// </remarks>
    public static void Pull(CVarService cvars, IWindowService window)
    {
        ArgumentNullException.ThrowIfNull(cvars);
        ArgumentNullException.ThrowIfNull(window);

        window.Fullscreen = cvars.Get<bool>(Fullscreen);
        window.VSync = cvars.Get<bool>(VSync);
        window.FpsLimit = cvars.Get<int>(Fps);
    }
}
