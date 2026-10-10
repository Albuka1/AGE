using Age.Core;
using Age.Input;

namespace Age.Rendering;

/// <summary>
/// The developer console of a game: an overlay that drops from the top of the frame, takes the typed line, runs it, and lists
/// the commands that match what is being typed.
/// </summary>
/// <remarks>
/// <para>
/// The console is a render pass of its own rather than part of <see cref="DevOverlay"/>, because the two answer different
/// questions: the numbers of a frame are read while the game runs, and the console takes the whole input while it is open. A game
/// adds both to its pipeline, sets <see cref="OpenKey"/>, and reads <see cref="IConsoleService.IsOpen"/> to keep its own input
/// away from the game while the console holds it.
/// </para>
/// <para>
/// The panel slides down while it opens and up while it closes, over <see cref="AnimationDuration"/> seconds, which is what makes
/// it arrive rather than appear. A line that is being typed is completed with Tab to the name that matches, and the commands
/// that match are listed under it: a list of one is completed outright, and a list of several is walked with the up and down keys
/// and taken with Tab.
/// </para>
/// <para>
/// The title of the panel is read from the content through <see cref="ITextSource"/>: the key is <c>console-title</c>, and a game
/// that ships none reads the key itself, which is readable English.
/// </para>
/// </remarks>
public sealed partial class DevConsoleOverlay : IRenderPass
{
    /// <summary>The dark panel behind the lines, which the game is visible through.</summary>
    private static readonly Color PanelColour = new(18, 18, 18);

    /// <summary>The line under the header, which separates the title from the output.</summary>
    private static readonly Color BorderColour = new(90, 90, 90);

    /// <summary>The colour of the title of the panel.</summary>
    private static readonly Color TitleColour = new(180, 200, 255);

    /// <summary>The colour of a line of the output and of the line that is being typed.</summary>
    private static readonly Color TextColour = new(230, 230, 230);

    /// <summary>The colour of a failure.</summary>
    private static readonly Color ErrorColour = new(255, 120, 120);

    /// <summary>The colour of a warning.</summary>
    private static readonly Color WarningColour = new(255, 220, 120);

    /// <summary>The colour of the description of a suggestion.</summary>
    private static readonly Color HintColour = new(150, 150, 150);

    /// <summary>The colour of the row of a suggestion that is chosen.</summary>
    private static readonly Color SelectedColour = new(70, 90, 140);

    /// <summary>The padding between the edge of the panel and its lines, in pixels.</summary>
    private const float Padding = 10f;

    /// <summary>The height of the strip of the panel above the lines, which holds the title.</summary>
    private const float HeaderHeight = 26f;

    /// <summary>The gap between the name of a suggestion and its description, in pixels.</summary>
    private const float SuggestionGap = 12f;

    private readonly IConsoleService _console;
    private readonly IInputService _input;
    private readonly ITextInputService _text;
    private readonly FixedTimestep _timestep;
    private readonly IRenderer _renderer;
    private readonly TextRenderer? _textRenderer;
    private readonly ITextSource? _textSource;
    private readonly float _line;

    private double _frameTime;
    private double _openedAt;
    private double _closedAt;
    private bool _wasPaused;
    private bool _closing;
    private int _suggested = -1;
    private int _visibleLines = 14;
    private int _visibleSuggestions = 8;

    /// <summary>Initializes the console overlay from the console it drives and the services it reads.</summary>
    /// <param name="console">The console whose lines are drawn and whose input the typed characters reach.</param>
    /// <param name="input">The keys that open the console, run a line, walk its history and its suggestions.</param>
    /// <param name="text">The characters that are typed, which reach the console while it is open.</param>
    /// <param name="timestep">The clock, which stands still while the console is open.</param>
    /// <param name="renderer">The renderer that the overlay draws with.</param>
    /// <param name="textRenderer">The renderer of the text of the console, which draws it with the fonts of the engine, or null to draw it with the built-in font.</param>
    /// <param name="textSource">The source of the strings of the panel, or null to draw the keys themselves, which is readable English for the one key this panel uses.</param>
    /// <exception cref="ArgumentNullException">One of the arguments that is not optional is null.</exception>
    public DevConsoleOverlay(IConsoleService console, IInputService input, ITextInputService text, FixedTimestep timestep, IRenderer renderer, TextRenderer? textRenderer = null, ITextSource? textSource = null)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(timestep);
        ArgumentNullException.ThrowIfNull(renderer);

        _console = console;
        _input = input;
        _text = text;
        _timestep = timestep;
        _renderer = renderer;
        _textRenderer = textRenderer;
        _textSource = textSource;
        _line = (textRenderer?.LineHeight ?? BitmapFontMetrics.GlyphHeight) + 2f;
    }
}
