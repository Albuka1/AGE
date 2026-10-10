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
    private readonly TextRenderer? _textRenderer;
    private readonly float _line;

    private int _selected = -1;
    private double _copiedAt = double.NegativeInfinity;

    /// <summary>Initializes the page from the console whose output it shows and the clipboard a line is copied to.</summary>
    /// <param name="console">The console whose lines are drawn.</param>
    /// <param name="clipboard">The clipboard a clicked line is copied to, or null to leave a click selecting only.</param>
    /// <param name="textRenderer">The renderer of the text, which measures and draws it with the fonts of the engine, or null to draw it with the built-in font.</param>
    /// <exception cref="ArgumentNullException">The console is null.</exception>
    public ConsoleTab(IConsoleService console, IClipboardService? clipboard = null, TextRenderer? textRenderer = null)
    {
        ArgumentNullException.ThrowIfNull(console);

        _console = console;
        _clipboard = clipboard;
        _textRenderer = textRenderer;
        _line = (textRenderer?.LineHeight ?? BitmapFontMetrics.GlyphHeight) + 2f;
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
        (int first, int capacity) = Visible(lines.Count, body);

        if (pointer.Position.Y < body.Y || pointer.Position.Y >= body.Y + (capacity * _line))
        {
            return;
        }

        int row = first + (int)((pointer.Position.Y - body.Y) / _line);

        if (row < first || row >= lines.Count)
        {
            return;
        }

        _selected = row;
        _copiedAt = frame.Total;

        if (_clipboard is not null)
        {
            _clipboard.SetText(lines[row].Text);
        }
    }

    /// <inheritdoc />
    public void Render(IRenderer renderer, Rect body)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        IReadOnlyList<ConsoleLine> lines = _console.Lines;
        (int first, int capacity) = Visible(lines.Count, body);

        for (int index = first; index < lines.Count; index++)
        {
            float y = body.Y + ((index - first) * _line);

            // The line that was clicked is drawn on a band of its own, which is what says that a click copied it.
            if (index == _selected)
            {
                renderer.DrawRectangle(new Rect(new Vector2(body.X, y), new Vector2(body.Width, _line)), SelectedColour);
            }

            Line(renderer, lines[index].Text, new Vector2(body.X, y), ColourOf(lines[index].Level));
        }

        if (lines.Count == 0)
        {
            Line(renderer, "(the console wrote nothing yet)", new Vector2(body.X, body.Y), HintColour);
        }
        else if (_selected >= 0 && _copiedAt > double.NegativeInfinity)
        {
            Line(renderer, "copied to the clipboard", new Vector2(body.X, body.Y + body.Height - _line), CopiedColour);
        }
    }

    /// <summary>Returns the first line that is drawn and how many of them fit, which is what a click is turned into a line by.</summary>
    /// <param name="count">The number of lines the console holds.</param>
    /// <param name="body">The rectangle the page owns.</param>
    /// <remarks>
    /// The lines are read from the newest backwards, so a page that is too short shows the end of the output rather than the beginning
    /// of it, which is where the answer to what was just done is.
    /// </remarks>
    private (int First, int Capacity) Visible(int count, Rect body)
    {
        int capacity = Math.Max(1, (int)(body.Height / _line));
        return (Math.Max(0, count - capacity), capacity);
    }

    /// <summary>Draws a line of the output with the fonts of the engine, or with the built-in font when there is none of them.</summary>
    private void Line(IRenderer renderer, string text, Vector2 position, Color color)
    {
        if (_textRenderer is null)
        {
            renderer.DrawText(text, position, color);
            return;
        }

        _textRenderer.Draw(text, position, new TextStyle { Color = color });
    }

    /// <summary>Returns the colour a line of the output is drawn in, which is what makes a failure and a warning stand out.</summary>
    private static Color ColourOf(ConsoleLevel level) => level switch
    {
        ConsoleLevel.Error => ErrorColour,
        ConsoleLevel.Warning => WarningColour,
        _ => TextColour,
    };
}
