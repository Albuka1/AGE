namespace Age.Input;

/// <summary>
/// A clipboard that holds its text in memory rather than on the machine, which is what a run with no window has.
/// </summary>
/// <remarks>
/// The text is kept for the length of the run, so a caller that copies and pastes reads back what it wrote, and nothing of it
/// reaches the clipboard of the account: a headless run and a test neither depend on what a person copied in another program nor
/// disturb it. Register it before <c>AddAgeSilkInput</c>, which replaces it with the clipboard of the window, or leave it in place
/// for a game that never opens one.
/// </remarks>
public sealed class NullClipboardService : IClipboardService
{
    private string _text = string.Empty;

    /// <inheritdoc />
    public string Text => _text;

    /// <inheritdoc />
    public void SetText(string? text) => _text = text ?? string.Empty;
}
