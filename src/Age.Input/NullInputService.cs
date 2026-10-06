using Age.Core;

namespace Age.Input;

/// <summary>
/// An input service that reports a single simulated pointer state and no keyboard input. Intended for tests and for
/// headless runs, where no window exists to poll.
/// </summary>
/// <remarks>
/// Keyboard queries always return <see langword="false"/>. <see cref="MousePosition"/> comes from <see cref="State"/>, and
/// the left mouse button reports <see cref="UIInputState.MouseDown"/> for both
/// <see cref="IInputService.IsMouseButtonDown"/> and <see cref="IInputService.IsMouseButtonPressed"/>; the service reads
/// the held state and never detects press transitions separately, so a press is reported on every frame the flag is set.
/// The right and middle buttons always report <see langword="false"/>. Set <see cref="State"/> before an update that
/// should see a press and clear the flag before an update that should not.
/// </remarks>
public sealed class NullInputService : IInputService
{
    /// <summary>Gets or sets the simulated pointer state. The default reports the pointer at the origin with no button held.</summary>
    public UIInputState State { get; set; }

    /// <inheritdoc />
    /// <remarks>This double has no frame boundary: it keeps reporting the held state while the flag is set.</remarks>
    public void BeginFrame()
    {
    }

    /// <inheritdoc />
    public Vector2 MousePosition => State.MousePosition;

    /// <inheritdoc />
    public bool IsKeyDown(Key key) => false;

    /// <inheritdoc />
    public bool IsKeyPressed(Key key) => false;

    /// <inheritdoc />
    public bool IsMouseButtonDown(MouseButton button) => button == MouseButton.Left && State.MouseDown;

    /// <inheritdoc />
    public bool IsMouseButtonPressed(MouseButton button) => button == MouseButton.Left && State.MouseDown;
}
