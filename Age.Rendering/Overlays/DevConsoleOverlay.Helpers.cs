using Age.Core;

namespace Age.Rendering;

/// <summary>The parts of the console panel that are not the drawing of a frame: the slide, the suggestions and the measuring.</summary>
public sealed partial class DevConsoleOverlay
{
    /// <summary>Returns how far the panel has slid into the frame, where one is fully in and zero is fully out.</summary>
    /// <remarks>
    /// The panel is in at once when the animation is turned off, and a closing panel is measured from the moment it started to
    /// close rather than from the start of the game loop, so a console that is opened and closed often still slides.
    /// </remarks>
    private float Slide()
    {
        if (!_console.IsOpen && !_closing)
        {
            return 0f;
        }

        if (AnimationDuration <= 0f)
        {
            return _console.IsOpen ? 1f : 0f;
        }

        if (!_closing)
        {
            return Math.Clamp((float)((_frameTime - _openedAt) / AnimationDuration), 0f, 1f);
        }

        float closing = Math.Clamp(1f - (float)((_frameTime - _closedAt) / AnimationDuration), 0f, 1f);

        if (closing <= 0f)
        {
            _closing = false;
        }

        return closing;
    }

    /// <summary>Returns the rows the panel lists under the line, which are the levels that could still be typed and the values a game offers for the word being typed.</summary>
    /// <remarks>
    /// A line that says nothing lists nothing, which is what keeps the rows of the panel out of the way until a developer starts to
    /// type: the list is what the line could become, and an empty line could become anything. The command rows are the levels one below
    /// the path the line walked, so a line of several words lists what follows it: <c>cvars</c> lists <c>cvars set</c>, and
    /// <c>cvars set </c> lists the cvar names the game offers. The value provider of the game adds what a word of an argument could
    /// be, so a line that ends in the name of a setting lists the values that setting takes.
    /// </remarks>
    private IReadOnlyList<Suggestion> Suggestions()
    {
        string line = _console.Input;

        // An empty line lists nothing: a console that showed every command the moment it opened would cover the output of the game
        // with rows before anything was typed, and the first character that is typed is what narrows the list down.
        if (string.IsNullOrWhiteSpace(line))
        {
            return [];
        }

        var rows = new List<Suggestion>();

        // The names come first, because they are what the line is mostly about, and the values of the game follow them.
        foreach (ConsoleCommand command in _console.Matches(line))
        {
            rows.Add(new Suggestion(command.Name, command.Description));
        }

        if (_values is not null)
        {
            foreach (string value in _values(line))
            {
                rows.Add(new Suggestion(value, string.Empty));
            }
        }

        return [.. rows.Take(_visibleSuggestions)];
    }

    /// <summary>Takes the chosen suggestion into the line being typed, which is what Tab does while the rows are listed.</summary>
    /// <returns><see langword="true"/> when a suggestion was taken, <see langword="false"/> when there is nothing to take.</returns>
    /// <remarks>
    /// A suggestion replaces the word that is being typed and keeps the words before it, so completing <c>cvars se</c> to the row
    /// <c>cvars set</c> leaves the line at <c>cvars set</c> and a value taken after it is added rather than put in place of the
    /// command.
    /// </remarks>
    private bool TakeSuggestion()
    {
        IReadOnlyList<Suggestion> suggestions = Suggestions();

        if (suggestions.Count == 0)
        {
            return false;
        }

        int index = _suggested < 0 ? 0 : Math.Min(_suggested, suggestions.Count - 1);
        string line = _console.Input;
        int end = line.AsSpan().LastIndexOfAny(' ', '\t');
        string chosen = suggestions[index].Text;

        // A name that holds several words is a whole line of its own, and a value is a word behind the path that is already typed.
        string completed = chosen.Contains(' ')
            ? chosen
            : $"{line[..(end + 1)]}{chosen}";

        _console.SetInput(completed);
        _suggested = -1;

        return true;
    }

    /// <summary>Moves the chosen row of the suggestions by a step and wraps around its ends.</summary>
    /// <param name="step">The step to move by, where one walks down the list and minus one walks up it.</param>
    /// <returns><see langword="true"/> when the list is there and the choice moved, so the key is not the history of the console.</returns>
    private bool MoveSuggestion(int step)
    {
        int count = Suggestions().Count;

        if (count == 0)
        {
            return false;
        }

        _suggested = (((_suggested + step) % count) + count) % count;
        return true;
    }

    /// <summary>Reads the control keys that edit the line of the console, which is what a copy, a cut, a paste and a select all do.</summary>
    /// <returns><see langword="true"/> when a key of this frame edited the line, so the rest of the keys are left alone.</returns>
    /// <remarks>
    /// A paste reads the clipboard before the line, so what a person copied in another window is a line of the console: nothing
    /// reaches the game and the text is written where the caret stands, over the selection when there is one.
    /// </remarks>
    private bool Edit()
    {
        // Select all marks the whole line, which is what a person does before copying it out or replacing it in one paste.
        if (_input.IsKeyPressed(Key.A))
        {
            _console.SelectAll();
            _suggested = -1;
            return true;
        }

        // Copy leaves the line as it was and puts what is selected on the clipboard: it never changes the line, so it is read before
        // the keys that do, and a copy of nothing does nothing.
        if (_input.IsKeyPressed(Key.C))
        {
            if (_console.SelectionLength > 0)
            {
                _clipboard?.SetText(_console.Selection);
            }

            return false;
        }

        // Cut is a copy that also takes the selection out of the line.
        if (_input.IsKeyPressed(Key.X))
        {
            if (_console.SelectionLength > 0)
            {
                _clipboard?.SetText(_console.Selection);
                _console.RemoveSelection();
            }

            return true;
        }

        // Paste writes the clipboard where the caret stands, which is what pasting over a selection replaces.
        if (_input.IsKeyPressed(Key.V))
        {
            if (_clipboard is not null)
            {
                _console.ReplaceSelection(_clipboard.Text);
            }

            return true;
        }

        return false;
    }

    /// <summary>Reads the keys that move the caret and, with Shift held, extend the selection rather than clearing it.</summary>
    /// <remarks>
    /// The caret is the place the next character lands and the selection is what a copy takes: holding Shift while an arrow key is
    /// pressed extends the selection from the anchor, and an arrow key pressed once without it clears the selection.
    /// </remarks>
    private void CaretKeys()
    {
        bool shift = _input.IsKeyDown(Key.ShiftLeft) || _input.IsKeyDown(Key.ShiftRight);
        bool moved = false;

        if (Repeats(Key.Left))
        {
            _console.MoveCaret(-1);
            moved = true;
        }
        else if (Repeats(Key.Right))
        {
            _console.MoveCaret(1);
            moved = true;
        }
        else if (_input.IsKeyPressed(Key.Home))
        {
            // Home and End take the caret to an end of the line and leave the anchor where it was, so Shift extends the selection to
            // that end; the setter of the caret is not used, because it would collapse the selection that Shift is holding.
            _console.MoveCaret(-_console.Caret);
            moved = true;
        }
        else if (_input.IsKeyPressed(Key.End))
        {
            _console.MoveCaret(_console.Input.Length - _console.Caret);
            moved = true;
        }

        // A move that is not extending the selection collapses it, which is what an arrow key without Shift does: the anchor follows
        // the caret to where it stopped. A move that is extending it leaves the anchor where the selection began.
        if (moved && !shift)
        {
            _console.SelectionAnchor = _console.Caret;
            _suggested = -1;
        }
    }

    /// <summary>Reads a drag of the pointer over the line that is being typed, which is what selects what it covers with the mouse.</summary>
    /// <returns><see langword="true"/> when the pointer of this frame touched the line, so the rest of the keys are left alone.</returns>
    /// <remarks>
    /// A press inside the line puts the caret where it stands and starts a selection from there, and holding the button drags the other
    /// end of it; letting go ends the drag. A press outside the line ends one that was going, which is what clicking away does.
    /// </remarks>
    private bool Drag()
    {
        Vector2 pointer = _input.MousePosition;
        bool down = _input.IsMouseButtonDown(MouseButton.Left);
        bool pressed = _input.IsMouseButtonPressed(MouseButton.Left);

        if (pressed && !Contains(_inputRect, pointer))
        {
            // A click that is not on the line ends the selection a previous drag left, so a click elsewhere is not a selection that
            // stays for the next copy.
            _selecting = false;
            return false;
        }

        if (pressed)
        {
            _selecting = true;

            // The caret is moved to where the press landed without clearing the selection: the anchor is set to the same place, so the
            // selection starts empty and grows as the pointer is dragged.
            _console.Caret = IndexAt(pointer.X);
            _console.SelectionAnchor = _console.Caret;
            return true;
        }

        if (!_selecting || !down)
        {
            _selecting = false;
            return false;
        }

        // The selection is extended by moving only the caret, so the anchor stays where the press landed and the two ends are the drag.
        _console.MoveCaret(IndexAt(pointer.X) - _console.Caret);
        return true;
    }

    /// <summary>Returns the position in a line that a horizontal position of the panel falls at, which is what a click is turned into.</summary>
    /// <param name="x">The position of the pointer, in the pixels of the frame.</param>
    /// <remarks>
    /// A position past an end of the line is taken to that end, so a drag that runs off the panel still selects to the end of the line
    /// rather than stopping short of it. A line that is measured one character at a time needs no more than the width of a character
    /// per step, which is what the loop walks.
    /// </remarks>
    private int IndexAt(float x)
    {
        string typed = _console.Input;

        for (var index = 1; index <= typed.Length; index++)
        {
            // The pointer is walked past one character at a time, and the first character whose right edge is past it says the position
            // the pointer is at, which is the gap it stands in.
            if (x < _inputRect.X + Measure(typed[..index]).X)
            {
                return index - 1;
            }
        }

        return typed.Length;
    }

    /// <summary>Returns a value indicating whether a point is inside a rectangle, which is what a press on the line is tested with.</summary>
    private static bool Contains(Rect rect, Vector2 point) =>
        point.X >= rect.X && point.X < rect.X + Math.Max(1f, rect.Width) && point.Y >= rect.Y && point.Y < rect.Y + rect.Height;


    /// <param name="Text">The text the row completes to, which is what Tab writes into the line.</param>
    /// <param name="Description">What the row means, which is empty for a value.</param>
    private readonly record struct Suggestion(string Text, string Description);

    /// <summary>Draws a line of the panel with the fonts of the game, or with the built-in font when it has none.</summary>
    /// <param name="text">The characters of the line.</param>
    /// <param name="position">The top-left corner of the line.</param>
    /// <param name="color">The colour of the glyphs.</param>
    /// <returns>The size the line takes, whose width is what places the caret and the description of a row.</returns>
    private Vector2 Write(string text, Vector2 position, Color color)
    {
        if (_textRenderer is null)
        {
            _renderer.DrawText(text, position, color);

            // The built-in font is a bitmap whose glyphs are all the same width, so a line is measured rather than laid out.
            return new Vector2(text.Length * BitmapFontMetrics.GlyphWidth, BitmapFontMetrics.GlyphHeight);
        }

        return _textRenderer.Draw(text, position, new TextStyle { Color = color });
    }

    /// <summary>Breaks a line of the output into rows that fit the width, which is what makes the panel wrap rather than overflow.</summary>
    /// <param name="text">The line to break.</param>
    /// <param name="width">The width the rows are kept inside, in pixels.</param>
    /// <returns>The rows, in order. An empty line is one empty row.</returns>
    /// <remarks>
    /// A row is broken on a word, which is what a log line of a developer wants: an identifier that would not fit whole is left
    /// on a row of its own rather than cut, because there is nowhere to break it, and a row that is still too wide is drawn as it
    /// is. The break is measured with the font the panel draws with, so a row that fits the measurement fits the screen.
    /// </remarks>
    private List<string> Break(string text, float width)
    {
        var rows = new List<string>();

        if (text.Length == 0)
        {
            rows.Add(string.Empty);
            return rows;
        }

        string row = string.Empty;

        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = row.Length == 0 ? word : $"{row} {word}";

            if (row.Length > 0 && Measure(candidate).X > width)
            {
                rows.Add(row);
                row = word;
                continue;
            }

            row = candidate;
        }

        if (row.Length > 0)
        {
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>Returns the width a line takes, which is what breaks it into rows that fit the window.</summary>
    /// <param name="text">The line to measure.</param>
    /// <remarks>
    /// The line is measured with the font the panel draws it in, so a row that fit the measurement is a row on the screen. A panel
    /// that has no engine font measures with the built-in one, whose glyphs are all the same width.
    /// </remarks>
    private Vector2 Measure(string text)
    {
        if (_textRenderer is null)
        {
            return new Vector2(text.Length * BitmapFontMetrics.GlyphWidth, BitmapFontMetrics.GlyphHeight);
        }

        return _textRenderer.Measure(text, new TextStyle());
    }

    /// <summary>Returns the title of the panel in the language of the game, or the key itself when the game ships no string for it.</summary>
    private string ResolveTitle() => _textSource?.Resolve("ui-console-title") ?? "ui-console-title";

    /// <summary>Returns the colour a line of the output is drawn in, which is what makes a failure and a warning stand out.</summary>
    private static Color ColourOf(ConsoleLevel level) => level switch
    {
        ConsoleLevel.Error => ErrorColour,
        ConsoleLevel.Warning => WarningColour,
        _ => TextColour,
    };
}
