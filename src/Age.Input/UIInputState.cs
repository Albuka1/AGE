using Age.Core;

namespace Age.Input;

/// <summary>
/// A snapshot of the pointer state used by UI systems.
/// </summary>
/// <param name="MousePosition">The pointer position, in screen coordinates.</param>
/// <param name="MouseDown">A value indicating whether the primary mouse button is held down.</param>
public readonly record struct UIInputState(Vector2 MousePosition, bool MouseDown);
