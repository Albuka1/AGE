using System.Runtime.ExceptionServices;
using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Releases what the rendering assembly holds: the splash, then the atlases and the textures of a frame, and then the
/// renderer that uploaded them.
/// </summary>
/// <remarks>
/// The window comes later, at <see cref="WindowShutdownStep"/>, because the device objects of this step live in its
/// context: releasing them after it was closed would touch a context that is gone.
/// </remarks>
internal sealed class RenderingShutdownStep : IGameShutdownStep
{
    private readonly SplashScreen _splash;
    private readonly IFontService _fonts;
    private readonly ITextureService _textures;
    private readonly IRenderer _renderer;

    /// <summary>Initializes the step from the services that own the device objects.</summary>
    /// <param name="splash">The splash, which owns the logo texture it uploaded.</param>
    /// <param name="fonts">The fonts, which own the atlases they baked.</param>
    /// <param name="textures">The textures, which own what they decoded.</param>
    /// <param name="renderer">The renderer, which owns the device itself.</param>
    public RenderingShutdownStep(SplashScreen splash, IFontService fonts, ITextureService textures, IRenderer renderer)
    {
        _splash = splash;
        _fonts = fonts;
        _textures = textures;
        _renderer = renderer;
    }

    /// <inheritdoc />
    public int Order => 500;

    /// <inheritdoc />
    /// <remarks>
    /// Every release is attempted even when one of them fails, because a shutdown that stopped at the first failure would
    /// leak the device objects of everything behind it. The first failure is thrown once all of them ran, which is the
    /// point at which the caller learns that the frame was not left clean.
    /// </remarks>
    public void Shutdown()
    {
        ExceptionDispatchInfo? failure = null;

        Attempt(() => _splash.Dispose());
        Attempt(() => _fonts.UnloadAll());
        Attempt(() => _textures.UnloadAll());
        Attempt(() => _renderer.Dispose());

        failure?.Throw();

        void Attempt(Action release)
        {
            try
            {
                release();
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }
    }
}
