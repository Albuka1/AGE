using Age.Core;

namespace Age.Rendering;

/// <summary>Draws the panel of the console, which is the half of it that is not reading keys.</summary>
public sealed partial class DevConsoleOverlay
{
    /// <summary>Draws the panel over the frame, sliding it in while it opens and out while it closes.</summary>
    /// <param name="world">The world of the frame, which the panel does not read: a console reports the engine rather than the game.</param>
    /// <param name="camera">The camera of the frame, which the panel does not read either, because it draws in the pixels of the window.</param>
    /// <remarks>
    /// Every line of the output wraps into the width of the window, so a long line of a log continues on the next row rather than
    /// running off the edge: what the panel shows is the text, not the first characters of it. The rows are counted after the
    /// wrap, which is what gives the panel its height and keeps the newest lines in view.
    /// </remarks>
    public void Render(World world, in Camera2D camera)
    {
        ArgumentNullException.ThrowIfNull(world);

        float slide = Slide();

        if (slide <= 0f)
        {
            _panelHeight = 0f;
            return;
        }

        Vector2 viewport = _renderer.ViewportSize;
        float width = viewport.X - (2f * Padding);
        IReadOnlyList<ConsoleCommand> suggestions = Suggestions();

        // The output is wrapped first, because the height of the panel is what places its top edge: a panel that grew by a row
        // has to move up rather than draw over the game below it.
        var rows = new List<(string Text, Color Colour)>();

        int shown = 0;

        for (int index = _console.Lines.Count - 1; index >= 0 && shown < _visibleLines; index--)
        {
            ConsoleLine entry = _console.Lines[index];

            // The lines are read from the newest backwards, so a panel that is too short shows the end of the output rather than
            // the start of it, which is where the answer to what was just typed is.
            List<string> wrapped = Wrap(entry.Text, width).ToList();

            shown += wrapped.Count;

            for (int part = wrapped.Count - 1; part >= 0; part--)
            {
                rows.Insert(0, (wrapped[part], ColourOf(entry.Level)));
            }
        }

        _renderer.BeginFrame(false);

        float height = HeaderHeight + Padding + ((rows.Count + 1 + suggestions.Count) * _line) + Padding;
        float top = (slide * height) - height;

        // The height that is on screen is what a game offsets its own overlay by: while the panel slides the offset follows the
        // edge of the panel rather than the height it will have when it is open.
        _panelHeight = slide * height;

        _renderer.DrawRectangle(new Rect(new Vector2(0f, top), new Vector2(viewport.X, height)), PanelColour);
        _renderer.DrawRectangle(new Rect(new Vector2(0f, top + HeaderHeight), new Vector2(viewport.X, 1f)), BorderColour);

        Write(ResolveTitle(), new Vector2(Padding, top + Padding), TitleColour);

        float y = top + HeaderHeight + Padding;

        foreach ((string text, Color colour) in rows)
        {
            Write(text, new Vector2(Padding, y), colour);
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
