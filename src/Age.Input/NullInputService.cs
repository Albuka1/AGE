using Age.Core;

namespace Age.Input;

/// <summary>
/// An input service that reports a single simulated pointer state and no keyboard input. Intended for tests and for
/// headless runs, where no window exists to poll.
/// </summary>
/// <remarks>
/// Keyboard queries always return <see langword="false"/>. <see cref="MousePosition"/> comes from <see cref="State"/>, and
/// the left mouse button reports <see cref="UIInputState.MouseDown"/> for both its down and its pressed query, while the
/// right and middle buttons always report <see langword="false"/>. Because the state is a single flag it cannot express a
/// press that lasts one frame only, so set it before the update that should see the press.
/// </remarks>
public sealed class NullInputService : IInputService
{
    /// <summary>Gets or sets the simulated pointer state. The default reports the pointer at the origin with no button held.</summary>
    public UIInputState State { get; set; }

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
