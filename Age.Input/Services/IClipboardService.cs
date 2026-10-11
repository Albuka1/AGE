namespace Age.Input;

/// <summary>
/// The clipboard of the machine, which is what a console or a text field copies a selection to and pastes from.
/// </summary>
/// <remarks>
/// <para>
/// The clipboard is shared with every other program of the account, so what a game reads is what a person put there in another
/// window, and what it writes can be read by one as well. That is the whole point: a path, a command or a line of a log is copied
/// out of a game and into wherever it is being looked at, and pasted back in.
/// </para>
/// <para>
/// A service that has no device behind it, such as a headless run or a test, holds the text itself rather than reaching a machine,
/// so a caller that copies and pastes in a row reads back what it wrote without a window.
/// </para>
/// </remarks>
public interface IClipboardService
{
    /// <summary>Gets the text of the clipboard, or an empty string when it holds no text at all.</summary>
    string Text { get; }

    /// <summary>Puts a text on the clipboard, replacing what was there.</summary>
    /// <param name="text">The text to hold, which replaces the whole of the clipboard. A null text holds an empty one.</param>
    void SetText(string? text);
}
