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
    /// wrap, which is what gives the panel its height and keeps the newest lines in view. The output scrolls with the page up and
    /// page down keys, which is what lets a developer read a log that is longer than the panel.
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
        IReadOnlyList<Suggestion> suggestions = Suggestions();

        // The output is wrapped first, because the height of the panel is what places its top edge: a panel that grew by a row has
        // to move up rather than draw over the game below it.
        var rows = new List<(string Text, Color Colour)>();

        foreach (ConsoleLine entry in _console.Lines)
        {
            foreach (string part in Break(entry.Text, width))
            {
                rows.Add((part, ColourOf(entry.Level)));
            }
        }

        // The output is read from its end, so the panel shows the answer to what was just typed rather than the start of the log,
        // and the scroll walks back through it. The offset is clamped to what there is, so scrolling past an end settles on it.
        int capacity = Math.Max(1, _visibleLines - suggestions.Count - 1);
        int maximum = Math.Max(0, rows.Count - capacity);
        _scrolled = Math.Clamp(_scrolled, 0, maximum);

        int end = rows.Count - _scrolled;
        int start = Math.Max(0, end - capacity);

        _renderer.BeginFrame(false);

        int count = end - start;
        float height = HeaderHeight + Padding + ((count + 1 + suggestions.Count) * _line) + Padding;
        float top = (slide * height) - height;

        // The height that is on screen is what a game offsets its own overlay by: while the panel slides the offset follows the
        // edge of the panel rather than the height it will have when it is open.
        _panelHeight = slide * height;

        _renderer.DrawRectangle(new Rect(new Vector2(0f, top), new Vector2(viewport.X, height)), PanelColour);
        _renderer.DrawRectangle(new Rect(new Vector2(0f, top + HeaderHeight), new Vector2(viewport.X, 1f)), BorderColour);

        Write(ResolveTitle(), new Vector2(Padding, top + Padding), TitleColour);

        float y = top + HeaderHeight + Padding;

        for (int index = start; index < end; index++)
        {
            (string text, Color colour) = rows[index];
            Write(text, new Vector2(Padding, y), colour);
            y += _line;
        }

        // The line that is being typed is drawn in three parts, because a selection is drawn behind the text it covers and the caret
        // stands where the next character lands: the prompt, the text before what is selected, the selection, the text after it. The
        // width of each part is measured with the font the panel draws with, which is what places the two.
        const string prompt = "> ";
        float lineStart = Padding + Measure(prompt).X;
        int selectionStart = _console.SelectionStart;
        int selectionLength = _console.SelectionLength;
        string typed = _console.Input;

        Write(prompt, new Vector2(Padding, y), TextColour);

        // A selection is a band behind the text it covers, drawn before the text so the glyphs stand on top of it.
        if (selectionLength > 0)
        {
            float before = Measure(typed[..selectionStart]).X;
            float selected = Measure(typed.Substring(selectionStart, selectionLength)).X;
            _renderer.DrawRectangle(new Rect(new Vector2(lineStart + before, y), new Vector2(selected, _line)), SelectedColour);
        }

        Write(typed, new Vector2(lineStart, y), TextColour);

        // The caret is a small block where the next character lands, which is what a console of a terminal shows in place of a cursor.
        float caretX = lineStart + Measure(typed[..Math.Clamp(_console.Caret, 0, typed.Length)]).X;
        _renderer.DrawRectangle(new Rect(new Vector2(caretX, y + 2f), new Vector2(2f, _line - 6f)), TextColour);

        // The line that is typed is remembered as a rectangle, because a drag of the pointer is turned into a position in the line by
        // it: a frame reads the pointer before it draws, so the rectangle of the frame before is the best one it has, and a line of a
        // console does not move between two frames while it is being dragged over.
        _inputRect = new Rect(new Vector2(lineStart, y), new Vector2(Math.Max(1f, Measure(typed).X), _line));
        y += _line;

        for (int index = 0; index < suggestions.Count; index++)
        {
            Suggestion suggestion = suggestions[index];

            if (index == _suggested)
            {
                _renderer.DrawRectangle(new Rect(new Vector2(0f, y), new Vector2(viewport.X, _line)), SelectedColour);
            }

            Vector2 nameSize = Write(suggestion.Text, new Vector2(Padding, y), TextColour);

            if (suggestion.Description.Length > 0)
            {
                Write(suggestion.Description, new Vector2(Padding + nameSize.X + SuggestionGap, y), HintColour);
            }

            y += _line;
        }

        _renderer.EndFrame();
    }
}
