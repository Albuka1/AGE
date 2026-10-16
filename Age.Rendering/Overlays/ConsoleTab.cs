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
/// The rows are built on the frame that draws them, because what a page shows is the output of the run as it stands and the output
/// of a frame is what the frame before it wrote: a page that built its rows on a frame it did not draw would be a page a frame
/// behind the console it reports. The newest rows are the ones in view, so a page that is shorter than the output reads the answer
/// to what was just done rather than the beginning of the run; the rows move when the number of lines in the console changes, and a
/// person who is reading further back is not moved away from the row they are on.
/// </para>
/// <para>
/// A press that the bar of the page took is a press of the bar rather than a press of the row that stood under it: the drag of the
/// list and the choice of a row are the two things one press can do, and the bar is what the pointer reached when it landed on it.
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

    private readonly List<ConsoleRow> _rows = [];

    private int _selected = -1;
    private int _lastLineCount;
    private ScrollState _scroll;

    /// <summary>Initializes the page from the console whose output it shows and the clipboard a line is copied to.</summary>
    /// <param name="console">The console whose lines are drawn.</param>
    /// <param name="clipboard">The clipboard a clicked line is copied to, or null to leave a click selecting only.</param>
    /// <exception cref="ArgumentNullException">The console is null.</exception>
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
    public bool Closable { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// The rows are the ones the last frame drew, which is what a person clicked on: a click is turned into a row by the same
    /// rectangle the row was drawn in, so the line a person reaches for is the line they saw. The rows are rebuilt by
    /// <see cref="Render"/> on the frame that draws them, which is what keeps the output of a frame and the click of the next one
    /// in step.
    /// </remarks>
    public void Update(in GameTime frame, Rect body, in WindowPointer pointer)
    {
        // A press that the bar of the page took is a press of the bar rather than a press of the row that stood under it: the
        // drag of the list and the choice of a row are the two things one press can do, and the bar is what the pointer
        // reached when it landed on it.
        bool captured = _scroll.Update(pointer, body, _rows.Count, _line);

        if (captured || !pointer.Pressed || !Contains(body, pointer.Position))
        {
            return;
        }

        int row = _scroll.First + (int)((pointer.Position.Y - body.Y) / _line);

        if (row < 0 || row >= _rows.Count)
        {
            return;
        }

        ConsoleRow clicked = _rows[row];
        IReadOnlyList<ConsoleLine> lines = _console.Lines;

        if (clicked.Line < 0 || clicked.Line >= lines.Count)
        {
            return;
        }

        _selected = clicked.Line;

        if (_clipboard is not null)
        {
            _clipboard.SetText(lines[clicked.Line].Text);
        }
    }

    /// <inheritdoc />
    public void Render(IRenderer renderer, Rect body)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        Layout(body);

        // The rows move to the end of the output when the console wrote a line the frame before, which is where the answer to what
        // was just done is. A drag of the bar is not moved: a person who is reading further back is not carried away from the row
        // they are on by a line the game wrote in the meantime.
        if (_lastLineCount != _console.Lines.Count && !_scroll.Dragging)
        {
            _scroll.ToEnd(body, _rows.Count, _line);
        }

        _lastLineCount = _console.Lines.Count;

        int visible = ScrollState.Visible(body, _line);
        int count = Math.Min(_rows.Count, visible);
        int start = Math.Clamp(_scroll.First, 0, Math.Max(0, _rows.Count - count));

        renderer.PushClip(body);

        try
        {
            for (var index = 0; index < count; index++)
            {
                ConsoleRow row = _rows[start + index];
                float y = body.Y + (index * _line);

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
        }
        finally
        {
            renderer.PopClip();
        }

        _scroll.DrawBar(renderer, body, _rows.Count, _line);

        if (_selected >= 0 && _rows.Count > 0)
        {
            renderer.DrawText("copied to the clipboard", new Vector2(body.X, body.Y + body.Height - _line), CopiedColour);
        }
    }

    /// <summary>Fills <see cref="_rows"/> with every wrapped row of the output, oldest first.</summary>
    /// <param name="body">The rectangle the page owns.</param>
    /// <remarks>
    /// Every line is wrapped here rather than only the newest ones, because the bar of the page is what turns the page into one
    /// that reads back: a page that built only the rows that fit its body would have nothing to drag through when a person looks
    /// for what happened before the last frame. The rows of one line are pushed in the order they are read, so a wrapped line that
    /// fills three rows is three rows of the page that a person reads as one.
    /// </remarks>
    private void Layout(Rect body)
    {
        _rows.Clear();

        IReadOnlyList<ConsoleLine> lines = _console.Lines;

        for (var index = 0; index < lines.Count; index++)
        {
            ConsoleLine line = lines[index];
            IReadOnlyList<string> wrapped = Wrap(line.Text, body.Width);

            for (var row = 0; row < wrapped.Count; row++)
            {
                _rows.Add(new ConsoleRow(index, wrapped[row], line.Level));
            }
        }
    }

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

            while (start < text.Length && text[start] == ' ')
            {
                start++;
            }
        }

        return rows;
    }

    private static Color ColourOf(ConsoleLevel level) => level switch
    {
        ConsoleLevel.Error => ErrorColour,
        ConsoleLevel.Warning => WarningColour,
        _ => TextColour,
    };

    private static bool Contains(Rect rect, Vector2 point) =>
        point.X >= rect.X && point.X < rect.X + rect.Width && point.Y >= rect.Y && point.Y < rect.Y + rect.Height;

    private readonly record struct ConsoleRow(int Line, string Text, ConsoleLevel Level);
}
