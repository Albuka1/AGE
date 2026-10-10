using Age.Core;

namespace Age.Rendering;

/// <summary>Draws the panel of the console, which is the half of it that is not reading keys.</summary>
public sealed partial class DevConsoleOverlay
{
    /// <summary>Draws the panel over the frame, sliding it in while it opens and out while it closes.</summary>
    /// <param name="world">The world of the frame, which the panel does not read: a console reports the engine rather than the game.</param>
    /// <param name="camera">The camera of the frame, which the panel does not read either, because it draws in the pixels of the window.</param>
    /// <remarks>The overlay begins its own frame without clearing, so what the game drew stays behind it, and it draws nothing once the panel is gone.</remarks>
    public void Render(World world, in Camera2D camera)
    {
        ArgumentNullException.ThrowIfNull(world);

        float slide = Slide();

        if (slide <= 0f)
        {
            return;
        }

        _renderer.BeginFrame(false);

        Vector2 viewport = _renderer.ViewportSize;
        IReadOnlyList<ConsoleLine> lines = _console.Lines;
        IReadOnlyList<ConsoleCommand> suggestions = Suggestions();
        int shown = Math.Min(lines.Count, _visibleLines);

        float height = HeaderHeight + Padding + ((shown + 1 + suggestions.Count) * _line) + Padding;
        float top = (slide * height) - height;

        _renderer.DrawRectangle(new Rect(new Vector2(0f, top), new Vector2(viewport.X, height)), PanelColour);
        _renderer.DrawRectangle(new Rect(new Vector2(0f, top + HeaderHeight), new Vector2(viewport.X, 1f)), BorderColour);

        Write(ResolveTitle(), new Vector2(Padding, top + Padding), TitleColour);

        float y = top + HeaderHeight + Padding;

        for (int index = 0; index < shown; index++)
        {
            ConsoleLine entry = lines[lines.Count - shown + index];
            Write(entry.Text, new Vector2(Padding, y), ColourOf(entry.Level));
            y += _line;
        }

        string input = $"> {_console.Input}";
        Vector2 inputSize = Write(input, new Vector2(Padding, y), TextColour);

        // The caret is a small block after the line, which is what a console of a terminal shows in place of a cursor.
        _renderer.DrawRectangle(new Rect(new Vector2(Padding + inputSize.X, y + 2f), new Vector2(2f, _line - 6f)), TextColour);
        y += _line;

        for (int index = 0; index < suggestions.Count; index++)
        {
            ConsoleCommand command = suggestions[index];

            if (index == _suggested)
            {
                _renderer.DrawRectangle(new Rect(new Vector2(0f, y), new Vector2(viewport.X, _line)), SelectedColour);
            }

            Vector2 nameSize = Write(command.Name, new Vector2(Padding, y), TextColour);
            Write(command.Description, new Vector2(Padding + nameSize.X + SuggestionGap, y), HintColour);
            y += _line;
        }

        _renderer.EndFrame();
    }
}
