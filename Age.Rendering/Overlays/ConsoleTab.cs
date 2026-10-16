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
/// ones, which is where the answer to what was just done is. A wheel over the body scrolls the output: a person scrolls up to read
/// what happened before, and the newest line is drawn again when the scroll is brought back to zero.
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
    public void Update(in GameTime frame, Rect body, in WindowPointer pointer)
    {
        Layout(body);
        _scroll.Update(pointer, body, _rows.Count, _line);

        if (!pointer.Pressed || !Contains(body, pointer.Position))
        {
            return;
        }

        // A press on a row is a press on the line that row came from, which is what the click selects and the clipboard is
        // written with. The rows of one line are counted with the row a person sees, so a wrapped line is a single line of the
        // console that a click on any of its rows reaches.
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
    /// Every line is wrapped here rather than only the newest ones, because a wheel over the body is what turns the page into one
    /// that reads back: a page that built only the rows that fit its body would have nothing to scroll through when a person looks
    /// for what happened before the last frame. The rows of one line are pushed in the order they are read, so a wrapped line that
    /// fills three rows is three rows of the page that a person scrolls through as one.
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

        // The scroll is brought back to the newest line when the page is drawn for the first time, which is what a console that
        // has just been opened reads: a page that started at the oldest line would show the beginning of the run rather than what
        // the game just wrote. A person who scrolls the page away from the end keeps the row they are reading, and the newest line
        // is drawn again when they bring the scroll back to it.
        if (lines.Count > 0)
        {
            _scroll.ToEnd(body, _rows.Count, _line);
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
