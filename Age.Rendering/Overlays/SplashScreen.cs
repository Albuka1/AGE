using Age.Assets;
using Age.Core;
using Age.Input;

namespace Age.Rendering;

/// <summary>
/// Shows the logo of the engine on the screen before the game draws its own first frame. The game calls
/// <see cref="Draw"/> at the top of its tick and returns while the call returns <see langword="true"/>, so the splash
/// owns the frame for as long as it runs.
/// </summary>
/// <example>
/// <code>
/// SplashScreen splash = provider.GetRequiredService&lt;SplashScreen&gt;();
///
/// gameLoop.Run(time =>
/// {
///     if (splash.Draw(renderer, time, input))
///     {
///         return;
///     }
///
///     world.Update(time, pipeline);
/// });
/// </code>
/// </example>
/// <remarks>
/// By default the splash shows the built-in logo of the engine on a black screen, which suits a logo without a
/// background. Give <see cref="Logo"/> a texture of the game's own to replace it, size it with <see cref="LogoSize"/>,
/// and set <see cref="Enabled"/> to <see langword="false"/> to start the game straight away.
/// </remarks>
public sealed class SplashScreen : IDisposable
{
    private const float DefaultLogoScale = 320f / 720f;

    /// <summary>The share of the width of the logo that the bar under it covers.</summary>
    private const float ProgressBarScale = 0.9f;

    /// <summary>The distance between the bottom of the logo and the bar, in pixels.</summary>
    private const float ProgressBarGap = 32f;

    /// <summary>The height of the bar, in pixels.</summary>
    private const float ProgressBarHeight = 6f;

    /// <summary>The colour of the track the bar is filled along.</summary>
    private static readonly Color ProgressTrackColour = new(60, 60, 60);

    /// <summary>The colour of the part of the bar that has filled.</summary>
    private static readonly Color ProgressFillColour = new(230, 230, 230);

    private IRenderer? _renderer;
    private TextureHandle? _builtInLogo;
    private bool _ended;

    /// <summary>Gets or sets a value indicating whether the splash runs at all. The default is <see langword="true"/>.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets how long the logo stays on screen, measured from the start of the game loop. The default is 2.5 seconds.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(2.5);

    /// <summary>Gets or sets the size of the logo, in pixels.</summary>
    /// <remarks>
    /// A null value, which is the default, gives the logo a share of the viewport and computes it on every frame, so the
    /// splash keeps its shape after the window was resized. Set a size to draw the logo at exactly that many pixels.
    /// </remarks>
    public Vector2? LogoSize { get; set; }

    /// <summary>Gets or sets a value indicating whether Space, Enter, Escape or a left mouse click ends the splash early. The default is <see langword="true"/>.</summary>
    public bool SkipOnInput { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether a bar under the logo shows how far the loading behind it got. The default is <see langword="true"/>.</summary>
    /// <remarks>The bar is drawn only while <see cref="Progress"/> is below one, so a game that loads nothing keeps its logo alone.</remarks>
    public bool ShowProgress { get; set; } = true;

    /// <summary>Gets or sets how far the loading that runs behind the logo got, where zero is nothing and one is everything.</summary>
    /// <remarks>
    /// The value is a share and not a count, because the splash loads nothing itself: a game advances it as the steps of its
    /// own loading finish, and a value below zero or above one is drawn as the end it is nearer to. A game that keeps the
    /// value at one draws the logo as it was before there was a bar.
    /// </remarks>
    public float Progress { get; set; }

    /// <summary>Gets or sets the texture to show instead of the built-in logo. A null value shows the built-in logo.</summary>
    /// <remarks>The logo belongs to the caller when it is set here, so the splash never releases it.</remarks>
    public TextureHandle? Logo { get; set; }

    /// <summary>Ends the splash before its duration ran out.</summary>
    /// <remarks>Use it to start the game right after its assets were loaded, instead of waiting for the remaining time.</remarks>
    public void End() => _ended = true;

    /// <summary>Draws the logo over a cleared screen, for as long as the splash runs.</summary>
    /// <param name="renderer">The renderer to draw with.</param>
    /// <param name="time">The time of the current frame, measured from the start of the game loop.</param>
    /// <param name="input">The input that the skip request is read from. A null value skips the request.</param>
    /// <returns><see langword="true"/> when the splash drew on this frame, so the game should skip its own drawing.</returns>
    /// <remarks>
    /// The screen is always cleared, so the frame shows the logo on black while the game renders nothing. The frame that
    /// carries the skip request still belongs to the splash, so the game never sees the key or the click that ended it.
    /// </remarks>
    public bool Draw(IRenderer renderer, GameTime time, IInputService? input = null)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        if (!Enabled || _ended)
        {
            return false;
        }

        if (time.Total >= Duration.TotalSeconds)
        {
            _ended = true;
            return false;
        }

        bool skipped = SkipOnInput && input is not null && Skips(input);

        _renderer = renderer;
        TextureHandle logo = Logo ?? (_builtInLogo ??= UploadBuiltInLogo(renderer));

        var camera = new Camera2D
        {
            Position = Vector2.Zero,
            Zoom = 1f,
            ViewportSize = renderer.ViewportSize,
        };

        renderer.SetCamera(camera);
        renderer.BeginFrame(true);

        // The size of a logo the game did not size itself is computed from the viewport of this frame: the built-in logo
        // is a square that covers 320 pixels of a viewport 720 pixels tall, and it keeps that share of the shorter side
        // of the window whatever the window does.
        float side = MathF.Min(renderer.ViewportSize.X, renderer.ViewportSize.Y) * DefaultLogoScale;
        Vector2 logoSize = LogoSize ?? new Vector2(side, side);
        Vector2 logoPosition = (renderer.ViewportSize - logoSize) * 0.5f;
        renderer.DrawSprite(logo, logoPosition, logoSize, Color.White);

        if (ShowProgress)
        {
            DrawProgressBar(renderer, logoSize, logoPosition);
        }

        renderer.EndFrame();

        if (skipped)
        {
            _ended = true;
        }

        return true;
    }

    /// <summary>Releases the built-in logo that the splash uploaded. The splash cannot be used afterwards.</summary>
    /// <remarks>A logo the game supplied through <see cref="Logo"/> stays alive, because the game owns it.</remarks>
    public void Dispose()
    {
        if (_renderer is not null && _builtInLogo is TextureHandle logo)
        {
            _renderer.ReleaseTexture(logo);
        }

        _builtInLogo = null;
        _renderer = null;
    }

    /// <summary>Uploads the embedded logo of the engine on first use.</summary>
    private static TextureHandle UploadBuiltInLogo(IRenderer renderer)
    {
        ImageData logo = BuiltInBranding.Logo;
        return renderer.CreateTexture(logo.Pixels, logo.Width, logo.Height);
    }

    /// <summary>Draws the bar that shows how far the loading behind the logo got.</summary>
    /// <param name="renderer">The renderer to draw with.</param>
    /// <param name="logoSize">The size the logo was drawn at, which gives the bar its width and its place.</param>
    /// <param name="logoPosition">The top-left corner of the logo, which is what the bar hangs under.</param>
    /// <remarks>
    /// The bar is the width of the logo and a few pixels tall, centered under it, and it is two rectangles: the track under
    /// the fill, so a bar of a share below one still reads as a bar rather than as a short line. A share of zero draws the
    /// track alone, which is what a game that has not started loading yet shows.
    /// </remarks>
    private void DrawProgressBar(IRenderer renderer, Vector2 logoSize, Vector2 logoPosition)
    {
        float share = Math.Clamp(Progress, 0f, 1f);
        float width = logoSize.X * ProgressBarScale;
        var track = new Rect(
            new Vector2((renderer.ViewportSize.X - width) * 0.5f, logoPosition.Y + logoSize.Y + ProgressBarGap),
            new Vector2(width, ProgressBarHeight));

        renderer.DrawRectangle(track, ProgressTrackColour);

        if (share > 0f)
        {
            renderer.DrawRectangle(new Rect(track.Position, new Vector2(width * share, track.Height)), ProgressFillColour);
        }
    }

    /// <summary>Determines whether the input asks for the splash to end.</summary>
    private static bool Skips(IInputService input) =>
        input.IsKeyPressed(Key.Space)
        || input.IsKeyPressed(Key.Enter)
        || input.IsKeyPressed(Key.Escape)
        || input.IsMouseButtonPressed(MouseButton.Left);
}
