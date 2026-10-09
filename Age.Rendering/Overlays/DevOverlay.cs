using Age.Core;
using Age.Input;

namespace Age.Rendering;

/// <summary>
/// The developer overlay of a game: the console and a few numbers about the frame, drawn over everything else.
/// </summary>
/// <remarks>
/// <para>
/// The overlay is a render pass, so it draws inside the frame of the game rather than next to it and is added after the
/// passes it covers. It draws with the built-in bitmap font, which every build has, so a game does not have to load a
/// font before it can look at itself. It also names the images that content asked for and the build does not have, which
/// is what makes a path with a typo in it visible without reading the log.
/// </para>
/// <para>
/// The stats key, F1 by default, shows and hides the numbers, and the console key, Tab by default, opens and closes the
/// console, which stands the simulation still while it is open and puts the clock back the way it was when it closes.
/// While the console is open the typed characters reach it, Enter runs the line, backspace removes a character, and the up
/// and down keys walk its history.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// renderPipeline.Add(provider.GetRequiredService&lt;DevOverlay&gt;());
///
/// gameLoop.Run(
///     update: step =&gt; world.Update(step, pipeline),
///     render: time =&gt;
///     {
///         world.UpdateFrame(time, pipeline);
///         overlay.Update(time);
///         renderPipeline.Render(world, camera);
///     });
/// </code>
/// </example>
public sealed class DevOverlay : IRenderPass
{
    private static readonly Color TextColour = new(230, 230, 230);
    private static readonly Color ErrorColour = new(255, 120, 120);
    private static readonly Color HintColour = new(150, 150, 150);

    private readonly IConsoleService _console;
    private readonly IInputService _input;
    private readonly ITextInputService _text;
    private readonly SystemPipeline _pipeline;
    private readonly FixedTimestep _timestep;
    private readonly IRenderer _renderer;
    private readonly ITextureService? _textures;

    private double _seconds;
    private double _framesPerSecond;
    private int _frames;
    private int _consoleLines = 12;
    private bool _wasPaused;

    /// <summary>Initializes the overlay from the console it drives and the services it reports.</summary>
    /// <param name="console">The console that <see cref="ConsoleKey"/> opens and the typed characters reach.</param>
    /// <param name="input">The keys that open the console, run a line and walk its history.</param>
    /// <param name="text">The characters that are typed, which reach the console while it is open.</param>
    /// <param name="pipeline">The pipeline whose step and frame times are printed.</param>
    /// <param name="timestep">The clock, which stands still while the console is open.</param>
    /// <param name="renderer">The renderer that the overlay draws with.</param>
    /// <param name="textures">The texture service, whose images that are not there are reported, or null to report none.</param>
    /// <exception cref="ArgumentNullException">One of the arguments is null.</exception>
    public DevOverlay(IConsoleService console, IInputService input, ITextInputService text, SystemPipeline pipeline, FixedTimestep timestep, IRenderer renderer, ITextureService? textures = null)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(timestep);
        ArgumentNullException.ThrowIfNull(renderer);

        _console = console;
        _input = input;
        _textures = textures;
        _text = text;
        _pipeline = pipeline;
        _timestep = timestep;
        _renderer = renderer;
    }

    /// <summary>Gets or sets a value indicating whether the numbers of the frame are drawn. <see cref="StatsKey"/> changes it.</summary>
    public bool ShowStats { get; set; } = true;

    /// <summary>Gets or sets the key that shows and hides the numbers of the frame. The default is F1.</summary>
    public Key StatsKey { get; set; } = Key.F1;

    /// <summary>Gets or sets the key that opens and closes the console. The default is Tab.</summary>
    /// <remarks>Escape closes the console as well, so a game that keeps Escape for a menu still has a way out.</remarks>
    public Key ConsoleKey { get; set; } = Key.Tab;

    /// <summary>Gets or sets the number of console lines that are drawn above the line that is being typed. The default is 12.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The number is negative.</exception>
    public int ConsoleLines
    {
        get => _consoleLines;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _consoleLines = value;
        }
    }

    /// <summary>Gets the frames per second over the last window of a second, which is what the overlay prints.</summary>
    public double FramesPerSecond => _framesPerSecond;

    /// <summary>Reads the keys and the characters of one frame, which is what opens the console and runs a line.</summary>
    /// <param name="frame">The time of the frame, which the frames per second are measured over.</param>
    /// <remarks>Call it once per frame from the render callback of the loop, before the passes are rendered.</remarks>
    public void Update(in GameTime frame)
    {
        Measure(frame.Delta);

        foreach (char character in _text.TypedCharacters)
        {
            if (_console.IsOpen)
            {
                _console.Type(character);
            }
        }

        if (_input.IsKeyPressed(StatsKey))
        {
            ShowStats = !ShowStats;
        }

        if (_input.IsKeyPressed(ConsoleKey))
        {
            _console.Toggle();
            ApplyPause();
        }

        if (!_console.IsOpen)
        {
            return;
        }

        if (_input.IsKeyPressed(Key.Escape))
        {
            _console.Close();
            ApplyPause();
            return;
        }

        if (_input.IsKeyPressed(Key.Enter))
        {
            _console.Submit();
        }

        if (_input.IsKeyPressed(Key.Backspace))
        {
            _console.Backspace();
        }

        if (_input.IsKeyPressed(Key.Up))
        {
            _console.RecallPrevious();
        }

        if (_input.IsKeyPressed(Key.Down))
        {
            _console.RecallNext();
        }
    }

    /// <summary>Counts a frame and works out the frames per second over a window, which is what a developer reads first.</summary>
    private void Measure(double delta)
    {
        _frames++;
        _seconds += delta;

        if (_seconds < 0.5d)
        {
            return;
        }

        _framesPerSecond = _frames / _seconds;
        _frames = 0;
        _seconds = 0d;
    }

    /// <summary>Stands the clock still while the console is open, and puts it back the way it was when it closes.</summary>
    /// <remarks>A game that was already paused stays paused, because the overlay remembers what it found.</remarks>
    private void ApplyPause()
    {
        if (_console.IsOpen)
        {
            _wasPaused = _timestep.Paused;
            _timestep.Paused = true;
            return;
        }

        _timestep.Paused = _wasPaused;
    }

    /// <inheritdoc />
    /// <remarks>The overlay begins its own frame without clearing, so what the game drew stays behind it.</remarks>
    public void Render(World world, in Camera2D camera)
    {
        ArgumentNullException.ThrowIfNull(world);

        Vector2 viewport = _renderer.ViewportSize;
        float y = 8f;

        _renderer.BeginFrame(false);

        if (_console.IsOpen)
        {
            y = DrawConsole(viewport, y);
        }

        if (ShowStats)
        {
            DrawStats(world, y);
        }

        _renderer.EndFrame();
    }

    /// <summary>Draws the visible lines of the console and the line that is being typed, and returns the line below them.</summary>
    private float DrawConsole(Vector2 viewport, float y)
    {
        float line = BitmapFontMetrics.GlyphHeight + 2f;
        IReadOnlyList<string> output = _console.Output;
        int shown = Math.Min(output.Count, ConsoleLines);

        _renderer.DrawRectangle(new Rect(Vector2.Zero, new Vector2(viewport.X, (shown + 2f) * line)), Color.Black);

        for (var index = 0; index < shown; index++)
        {
            string text = output[output.Count - shown + index];
            Color colour = text.StartsWith("error:", StringComparison.Ordinal) ? ErrorColour : TextColour;
            _renderer.DrawText(text, new Vector2(8f, 8f + (index * line)), colour);
        }

        _renderer.DrawText($"> {_console.Input}_", new Vector2(8f, 8f + (shown * line)), TextColour);

        return 8f + ((shown + 2f) * line);
    }

    /// <summary>Draws the numbers of the frame and of every system behind them.</summary>
    private void DrawStats(World world, float y)
    {
        float line = BitmapFontMetrics.GlyphHeight + 2f;
        string clock = _timestep.Paused ? "paused" : $"{_timestep.TimeScale:0.##}x";

        _renderer.DrawText(
            $"FPS {_framesPerSecond:0.0}  entities {world.Enumerate().Count()}  tick {_timestep.Tick}  {clock}",
            new Vector2(8f, y),
            TextColour);

        y += line;

        // A sprite whose image is not there is drawn as the placeholder, and this is where the reason is named: the paths
        // that could not be resolved, in the order the game asked for them.
        if (_textures?.MissingCount > 0)
        {
            _renderer.DrawText($"{_textures.MissingCount} missing images: {string.Join(", ", _textures.Missing)}", new Vector2(8f, y), ErrorColour);
            y += line;
        }

        y = DrawTimings(_pipeline.StepTimings, "step", y);
        y = DrawTimings(_pipeline.FrameTimings, "frame", y);
        _renderer.DrawText($"{StatsKey} numbers  {ConsoleKey} console", new Vector2(8f, y), HintColour);
    }

    /// <summary>Draws one line per system of a stage and returns the line below the last one.</summary>
    private float DrawTimings(IReadOnlyList<SystemTiming> timings, string stage, float y)
    {
        float line = BitmapFontMetrics.GlyphHeight + 2f;

        foreach (SystemTiming timing in timings)
        {
            _renderer.DrawText($"{stage}  {timing.Name}  {timing.Milliseconds:0.00} ms", new Vector2(8f, y), HintColour);
            y += line;
        }

        return y;
    }
}
