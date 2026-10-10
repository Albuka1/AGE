using Age.Core;
using Age.Input;

namespace Age.Rendering;

/// <summary>
/// The developer overlay of a game: a few numbers about the frame, drawn over everything else.
/// </summary>
/// <remarks>
/// <para>
/// The overlay is a render pass, so it draws inside the frame of the game rather than next to it and is added after the
/// passes it covers. It draws with the built-in bitmap font, which every build has, so a game does not have to load a
/// font before it can look at itself. It also names the images that content asked for and the build does not have, which
/// is what makes a path with a typo in it visible without reading the log.
/// </para>
/// <para>
/// The stats key, F3 by default, shows and hides the numbers. The console of a game is <see cref="DevConsoleOverlay"/>, which
/// is a pass of its own because it takes the whole input while it is open.
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

    private readonly IInputService _input;
    private readonly SystemPipeline _pipeline;
    private readonly FixedTimestep _timestep;
    private readonly IRenderer _renderer;
    private readonly ITextureService? _textures;
    private readonly ISpriteSheetService? _sheets;
    private readonly TextRenderer? _textRenderer;
    private readonly float _line;

    private double _seconds;
    private double _framesPerSecond;
    private int _frames;

    /// <summary>Initializes the overlay from the services it reports.</summary>
    /// <param name="input">The key that shows and hides the numbers.</param>
    /// <param name="pipeline">The pipeline whose step and frame times are printed.</param>
    /// <param name="timestep">The clock, whose tick and factor are printed.</param>
    /// <param name="renderer">The renderer that the overlay draws with.</param>
    /// <param name="textures">The texture service, whose images that are not there are reported, or null to report none.</param>
    /// <param name="sheets">The sheet service, whose sheets and states that did not resolve are reported, or null to report none.</param>
    /// <param name="textRenderer">The renderer of the text of the overlay, which draws it with the fonts of the engine, or null to draw it with the built-in font.</param>
    /// <exception cref="ArgumentNullException">One of the arguments is null.</exception>
    public DevOverlay(IInputService input, SystemPipeline pipeline, FixedTimestep timestep, IRenderer renderer, ITextureService? textures = null, ISpriteSheetService? sheets = null, TextRenderer? textRenderer = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(timestep);
        ArgumentNullException.ThrowIfNull(renderer);

        _input = input;
        _textures = textures;
        _sheets = sheets;
        _textRenderer = textRenderer;

        // The lines of the overlay are stacked at the height of a line of the font it draws with, so a font of the engine that
        // is taller than the bitmap one does not make the lines of the numbers overlap.
        _line = textRenderer?.LineHeight ?? BitmapFontMetrics.GlyphHeight;
        _pipeline = pipeline;
        _timestep = timestep;
        _renderer = renderer;
    }

    /// <summary>Gets or sets a value indicating whether the numbers of the frame are drawn. <see cref="StatsKey"/> changes it.</summary>
    public bool ShowStats { get; set; } = true;

    /// <summary>Gets or sets the key that shows and hides the numbers of the frame. The default is F3.</summary>
    public Key StatsKey { get; set; } = Key.F3;

    /// <summary>Gets the frames per second over the last window of a second, which is what the overlay prints.</summary>
    public double FramesPerSecond => _framesPerSecond;

    /// <summary>Reads the keys of one frame, which is what shows and hides the numbers.</summary>
    /// <param name="frame">The time of the frame, which the frames per second are measured over.</param>
    /// <remarks>Call it once per frame from the render callback of the loop, before the passes are rendered.</remarks>
    public void Update(in GameTime frame)
    {
        Measure(frame.Delta);

        if (_input.IsKeyPressed(StatsKey))
        {
            ShowStats = !ShowStats;
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

    /// <inheritdoc />
    /// <remarks>The overlay begins its own frame without clearing, so what the game drew stays behind it.</remarks>
    public void Render(World world, in Camera2D camera)
    {
        ArgumentNullException.ThrowIfNull(world);

        _renderer.BeginFrame(false);

        if (ShowStats)
        {
            DrawStats(world, 8f);
        }

        _renderer.EndFrame();
    }

    /// <summary>Draws a line of the overlay with the fonts of the engine, or with the built-in font when it has none.</summary>
    /// <param name="text">The characters of the line.</param>
    /// <param name="position">The top-left corner of the line.</param>
    /// <param name="color">The color of the glyphs.</param>
    /// <remarks>
    /// The font of the engine is what makes a line of a game readable in the numbers of a frame: the built-in font covers the
    /// printable ASCII range and draws everything else as a space, which is what a game that ships no font gets.
    /// </remarks>
    private void Line(string text, Vector2 position, Color color)
    {
        if (_textRenderer is null)
        {
            _renderer.DrawText(text, position, color);
            return;
        }

        _textRenderer.Draw(text, position, new TextStyle { Color = color });
    }

    /// <summary>Draws the numbers of the frame and of every system behind them.</summary>
    private void DrawStats(World world, float y)
    {
        float line = _line + 2f;
        string clock = _timestep.Paused ? "paused" : $"{_timestep.TimeScale:0.##}x";

        Line($"FPS {_framesPerSecond:0.0}  entities {world.Enumerate().Count()}  tick {_timestep.Tick}  {clock}", new Vector2(8f, y), TextColour);

        y += line;

        // A sprite whose image is not there is drawn as the placeholder, and this is where the reason is named: the paths
        // that could not be resolved, in the order the game asked for them.
        if (_textures?.MissingCount > 0)
        {
            Line($"{_textures.MissingCount} missing images: {string.Join(", ", _textures.Missing)}", new Vector2(8f, y), ErrorColour);
            y += line;
        }

        // A sheet that content names and the build does not have, and a state that a sheet does not declare, are drawn as the
        // placeholder as well, and this is where they are named: an entry of a sheet reads as 'Textures/Entities/goblin.yml'
        // or as 'Textures/Entities/goblin.yml:idle', a document and a state of it.
        if (_sheets is not null)
        {
            string unresolved = string.Join(", ", _sheets.Missing);

            if (unresolved.Length > 0)
            {
                Line($"{_sheets.Missing.Count()} sheets that did not resolve: {unresolved}", new Vector2(8f, y), ErrorColour);
                y += line;
            }
        }

        y = DrawTimings(_pipeline.StepTimings, "step", y);
        y = DrawTimings(_pipeline.FrameTimings, "frame", y);
        Line($"{StatsKey} numbers", new Vector2(8f, y), HintColour);
    }

    /// <summary>Draws one line per system of a stage and returns the line below the last one.</summary>
    private float DrawTimings(IReadOnlyList<SystemTiming> timings, string stage, float y)
    {
        float line = _line + 2f;

        foreach (SystemTiming timing in timings)
        {
            Line($"{stage}  {timing.Name}  {timing.Milliseconds:0.00} ms", new Vector2(8f, y), HintColour);
            y += line;
        }

        return y;
    }
}
