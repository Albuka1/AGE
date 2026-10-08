using Age.Core;

namespace Age.Input;

/// <summary>
/// An input service that reports a single simulated pointer state, no keyboard input and no typed characters. Intended for
/// tests and for headless runs, where no window exists to poll.
/// </summary>
/// <remarks>
/// Keyboard queries always return <see langword="false"/>. <see cref="MousePosition"/> comes from <see cref="State"/>, and
/// the left mouse button reports <see cref="UIInputState.MouseDown"/> for both
/// <see cref="IInputService.IsMouseButtonDown"/> and <see cref="IInputService.IsMouseButtonPressed"/>, with the press
/// reported only on the frame that follows one where the flag was still clear. The right and middle buttons always report
/// <see langword="false"/>, because <see cref="UIInputState"/> describes a single button. Set <see cref="State"/> before
/// the update that should see a press. Typed characters come from <see cref="Typed"/>, which a test sets to drive
/// something that reads words.
/// </remarks>
public sealed class NullInputService : IInputService, ITextInputService
{
    private bool _mouseDownLastFrame;
    private bool _mouseDownThisFrame;

    /// <summary>Gets or sets the simulated pointer state. The default reports the pointer at the origin with no button held.</summary>
    public UIInputState State { get; set; }

    /// <summary>Gets or sets the characters that <see cref="TypedCharacters"/> reports. The default reports none.</summary>
    public string Typed { get; set; } = string.Empty;

    /// <inheritdoc />
    public string TypedCharacters => Typed;

    /// <inheritdoc />
    public void BeginFrame()
    {
        _mouseDownLastFrame = _mouseDownThisFrame;
        _mouseDownThisFrame = State.MouseDown;
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
    /// <remarks>The press is reported on the first frame in which <see cref="State"/> holds the button, so the double follows the same transition rule as a service that reads a device.</remarks>
    public bool IsMouseButtonPressed(MouseButton button) => button == MouseButton.Left && _mouseDownThisFrame && !_mouseDownLastFrame;
}
