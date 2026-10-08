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
        renderer.DrawSprite(logo, (renderer.ViewportSize - logoSize) * 0.5f, logoSize, Color.White);
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

    /// <summary>Determines whether the input asks for the splash to end.</summary>
    private static bool Skips(IInputService input) =>
        input.IsKeyPressed(Key.Space)
        || input.IsKeyPressed(Key.Enter)
        || input.IsKeyPressed(Key.Escape)
        || input.IsMouseButtonPressed(MouseButton.Left);
}
