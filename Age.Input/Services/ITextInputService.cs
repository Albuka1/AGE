namespace Age.Input;

/// <summary>
/// Provides the characters that were typed during the current frame, which is what a console or a text field reads.
/// </summary>
/// <remarks>
/// <para>
/// The interface is separate from <see cref="IInputService"/> because it describes text rather than keys, and because a
/// service that has no device behind it cannot produce any: a system that reads words depends on this one as well, and
/// a service that has no keyboard reports no characters rather than being absent.
/// </para>
/// <para>
/// The characters of one frame are the ones the keyboard produced during it, in order, including the repeat of a held
/// key. A capital letter is reported the way the layout makes it, and a key that produces no character, such as an arrow
/// key, reports none: read <see cref="IInputService.IsKeyPressed"/> for those.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// foreach (char character in text.TypedCharacters)
/// {
///     console.Type(character);
/// }
/// </code>
/// </example>
public interface ITextInputService
{
    /// <summary>Gets the characters that were typed during the current frame, in order, which is empty when none were.</summary>
    string TypedCharacters { get; }
}
