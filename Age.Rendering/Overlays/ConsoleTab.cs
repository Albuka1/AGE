using Age.Core;
using Age.Input;

namespace Age.Rendering;

/// <summary>
/// The tab of the developer window that shows the output of the console: the lines the engine and the game wrote, newest at the
/// bottom, coloured by the severity they were written at.
/// </summary>
/// <remarks>
/// <para>
/// The console of the engine is a line that is typed, and this is what it wrote: a game reports through it and the engine sends what
/// it has to say through it, so the page is where a failure or a warning is read without opening the console itself and typing
/// anything. The tab is read only here: a line is typed in the console, which is a pass of its own.
/// </para>
/// <para>
/// The lines are drawn from the bottom up, so a page that is shorter than the output shows the newest lines rather than the oldest
/// ones, which is where the answer to what was just done is.
/// </para>
/// </remarks>
public sealed class ConsoleTab : IDevWindowTab
{
    private static readonly Color TextColour = new(225, 225, 225);
    private static readonly Color ErrorColour = new(255, 120, 120);
    private static readonly Color WarningColour = new(255, 220, 120);
    private static readonly Color HintColour = new(150, 150, 150);
    private static readonly Color SelectedColour = new(58, 58, 76);
    private static readonly Color CopiedColour = new(150, 220, 150);

    private readonly IConsoleService _console;
    private readonly IClipboardService? _clipboard;
    private readonly float _line;

    // The rows that were drawn on the last frame, which is what turns a click on a row back into the line it came from: a line that is
    // wrapped over several rows is one line, and every one of its rows copies it whole.
    private readonly List<ConsoleRow> _rows = [];

    private int _selected = -1;

    /// <summary>Initializes the page from the console whose output it shows and the clipboard a line is copied to.</summary>
    /// <param name="console">The console whose lines are drawn.</param>
    /// <param name="clipboard">The clipboard a clicked line is copied to, or null to leave a click selecting only.</param>
    /// <exception cref="ArgumentNullException">The console is null.</exception>
    /// <remarks>
    /// The page draws through the renderer it is handed, which is the one of the window it lives in rather than the one of the game:
    /// a text renderer of the engine draws into the context of the game, and a page of a window beside the game has a context of its
    /// own. The line height is therefore the one of the built-in font, which is what a window host draws its text with.
    /// </remarks>
    public ConsoleTab(IConsoleService console, IClipboardService? clipboard = null)
    {
        ArgumentNullException.ThrowIfNull(console);

        _console = console;
        _clipboard = clipboard;
        _line = BitmapFontMetrics.GlyphHeight + 4f;
    }

    /// <inheritdoc />
    public string Title => "console";

    /// <inheritdoc />
    /// <remarks>A page of the console stays by default, because it reports what the engine holds rather than a thing a person opened.</remarks>
    public bool Closable { get; set; }

    /// <inheritdoc />
    /// <remarks>A left click on a line copies it to the clipboard, which is what a line of a log is wanted for.</remarks>
    public void Update(in GameTime frame, Rect body, in WindowPointer pointer)
    {
        if (!pointer.Pressed)
        {
            return;
        }

        IReadOnlyList<ConsoleLine> lines = _console.Lines;

        if (At(pointer.Position, body) is not ConsoleRow row || row.Line < 0 || row.Line >= lines.Count)
        {
            return;
        }

        _selected = row.Line;

        if (_clipboard is not null)
        {
            _clipboard.SetText(lines[row.Line].Text);
        }
    }

    /// <inheritdoc />
    public void Render(IRenderer renderer, Rect body)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        Layout(body);

        for (int index = 0; index < _rows.Count; index++)
        {
            ConsoleRow row = _rows[index];
            float y = body.Y + (index * _line);

            // The line that was clicked is drawn on a band of its own, which is what says that a click copied it. Every row of a
            // wrapped line is banded, because the line is what was clicked rather than the row it happens to be on.
            if (row.Line == _selected)
            {
                renderer.DrawRectangle(new Rect(new Vector2(body.X, y), new Vector2(body.Width, _line)), SelectedColour);
            }

            renderer.DrawText(row.Text, new Vector2(body.X, y), ColourOf(row.Level));
        }

        if (_rows.Count == 0)
        {
            renderer.DrawText("(the console wrote nothing yet)", new Vector2(body.X, body.Y), HintColour);
        }
        else if (_selected >= 0)
        {
            renderer.DrawText("copied to the clipboard", new Vector2(body.X, body.Y + body.Height - _line), CopiedColour);
        }
    }

    /// <summary>Fills <see cref="_rows"/> with the rows of the newest lines that fit, wrapped to the width of the page.</summary>
    /// <param name="body">The rectangle the page owns.</param>
    /// <remarks>
    /// The lines are read from the newest backwards, so a page that is too short shows the end of the output rather than the
    /// beginning of it, which is where the answer to what was just done is. A line that is wider than the page is broken into rows,
    /// so a long line of a log is read rather than running off the side of the window.
    /// </remarks>
    private void Layout(Rect body)
    {
        _rows.Clear();

        IReadOnlyList<ConsoleLine> lines = _console.Lines;
        int capacity = Math.Max(1, (int)(body.Height / _line));

        // The rows of the newest lines are collected until the page is full, then the whole collection is reversed so the newest row
        // is the last one drawn: the output is read bottom-up, the way a console is.
        var collected = new List<ConsoleRow>();

        for (int index = lines.Count - 1; index >= 0 && collected.Count < capacity; index--)
        {
            ConsoleLine line = lines[index];
            IReadOnlyList<string> wrapped = Wrap(line.Text, body.Width);

            // The rows of one line are pushed in the order they are read, so the first row of a wrapped line comes before its last
            // one once the whole thing is reversed.
            for (int row = wrapped.Count - 1; row >= 0; row--)
            {
                collected.Add(new ConsoleRow(index, wrapped[row], line.Level));
            }
        }

        for (int index = collected.Count - 1; index >= 0; index--)
        {
            _rows.Add(collected[index]);
        }

        // Only the newest rows fit, so the ones beyond the page are dropped from the top rather than the bottom.
        if (_rows.Count > capacity)
        {
            _rows.RemoveRange(capacity, _rows.Count - capacity);
        }
    }

    /// <summary>Returns the row of the page that a point is over, or null when the point is outside the rows that were drawn.</summary>
    private ConsoleRow? At(Vector2 point, Rect body)
    {
        if (point.Y < body.Y || point.Y >= body.Y + (Math.Max(1, (int)(body.Height / _line)) * _line))
        {
            return null;
        }

        int index = (int)((point.Y - body.Y) / _line);
        return index >= 0 && index < _rows.Count ? _rows[index] : null;
    }

    /// <summary>Breaks a line of the output into the rows that fit the width of the page, breaking at a space where one is near the end.</summary>
    /// <param name="text">The line as the console holds it.</param>
    /// <param name="width">The width of the page in pixels.</param>
    /// <remarks>
    /// The text of the page is drawn in the built-in font, so a row holds as many characters as the width of the page divided by the
    /// width of a character, which is what the host measures a label with. A word that is longer than a row is broken where the row
    /// ends, because a row that is wider than the page runs off the side of the window rather than being read.
    /// </remarks>
    private static IReadOnlyList<string> Wrap(string text, float width)
    {
        int perRow = Math.Max(1, (int)(width / BitmapFontMetrics.GlyphWidth));

        if (text.Length <= perRow)
        {
            return [text];
        }

        var rows = new List<string>();
        int start = 0;

        while (start < text.Length)
        {
            int length = Math.Min(perRow, text.Length - start);

            // A break inside a word is worse than one between two of them, so the row is ended at the last space it holds when one
            // is far enough in to be worth it rather than leaving a row of a couple of characters.
            if (start + length < text.Length)
            {
                int space = text.LastIndexOf(' ', start + length - 1, length);

                if (space >= start + (perRow / 2))
                {
                    length = space - start;
                }
            }

            rows.Add(text.Substring(start, length).TrimEnd());
            start += length;

            // The space that ended a row is skipped rather than starting the next one with it.
            while (start < text.Length && text[start] == ' ')
            {
                start++;
            }
        }

        return rows;
    }

    /// <summary>Returns the colour a line of the output is drawn in, which is what makes a failure and a warning stand out.</summary>
    private static Color ColourOf(ConsoleLevel level) => level switch
    {
        ConsoleLevel.Error => ErrorColour,
        ConsoleLevel.Warning => WarningColour,
        _ => TextColour,
    };

    /// <summary>A row of the page: the text that is drawn on it and the line of the console it came from.</summary>
    private readonly record struct ConsoleRow(int Line, string Text, ConsoleLevel Level);
}
