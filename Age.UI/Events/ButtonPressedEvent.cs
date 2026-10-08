using Age.Core;

namespace Age.UI;

/// <summary>Raised when the pointer goes down on a button, in the frame in which it went down.</summary>
/// <param name="Button">The button that was pressed.</param>
/// <remarks>
/// A button is pressed once per click, so a game can act on the press without tracking the pointer itself. Holding the
/// button keeps <see cref="ButtonComponent.IsPressed"/> true, but raises nothing further.
/// </remarks>
public readonly record struct ButtonPressedEvent(Entity Button);
