using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Closes the window that owns the device context, which is the last thing a game releases.
/// </summary>
/// <remarks>
/// The order of this step is the highest of the engine, so everything that was drawn with the context of the window is
/// released before it goes.
/// </remarks>
internal sealed class WindowShutdownStep : IGameShutdownStep
{
    private readonly IWindowService _window;

    /// <summary>Initializes the step from the window of the game.</summary>
    /// <param name="window">The window to close.</param>
    public WindowShutdownStep(IWindowService window) => _window = window;

    /// <inheritdoc />
    public int Order => 900;

    /// <inheritdoc />
    public void Shutdown() => _window.Close();
}
