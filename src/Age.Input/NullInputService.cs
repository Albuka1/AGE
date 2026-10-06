using Age.Core;

namespace Age.Input;

/// <summary>
/// An input service for tests. Keyboard always false; MousePosition comes from State, IsMouseButtonDown(Left)
/// and IsMouseButtonPressed(Left) come from State.MouseDown, and Right and Middle are always false.
/// </summary>
public sealed class NullInputService : IInputService
{
    /// <summary>Gets or sets the simulated pointer state.</summary>
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
