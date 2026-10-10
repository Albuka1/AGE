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

    /// <summary>One row of the panel: what a line could become and what it means.</summary>
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
