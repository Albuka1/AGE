using Age.Core;

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

    private readonly IConsoleService _console;
    private readonly TextRenderer? _textRenderer;
    private readonly float _line;

    /// <summary>Initializes the tab from the console whose output it shows.</summary>
    /// <param name="console">The console whose lines are drawn.</param>
    /// <param name="textRenderer">The renderer of the text, which measures and draws it with the fonts of the engine, or null to draw it with the built-in font.</param>
    /// <exception cref="ArgumentNullException">The console is null.</exception>
    public ConsoleTab(IConsoleService console, TextRenderer? textRenderer = null)
    {
        ArgumentNullException.ThrowIfNull(console);

        _console = console;
        _textRenderer = textRenderer;
        _line = (textRenderer?.LineHeight ?? BitmapFontMetrics.GlyphHeight) + 2f;
    }

    /// <inheritdoc />
    public string Title => "console";

    /// <inheritdoc />
    /// <remarks>The output of the console is read from the tab and written in the console, so a frame of this tab reads nothing.</remarks>
    public void Update(in GameTime frame, Rect body)
    {
    }

    /// <inheritdoc />
    public void Render(IRenderer renderer, Rect body)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        IReadOnlyList<ConsoleLine> lines = _console.Lines;
        int capacity = Math.Max(1, (int)(body.Height / _line));

        // The lines are read from the newest backwards, so a page that is too short shows the end of the output rather than the
        // beginning of it, which is where the answer to what was just done is.
        int first = Math.Max(0, lines.Count - capacity);

        for (int index = first; index < lines.Count; index++)
        {
            float y = body.Y + ((index - first) * _line);
            Line(renderer, lines[index].Text, new Vector2(body.X, y), ColourOf(lines[index].Level));
        }

        if (lines.Count == 0)
        {
            Line(renderer, "(the console wrote nothing yet)", new Vector2(body.X, body.Y), HintColour);
        }
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
