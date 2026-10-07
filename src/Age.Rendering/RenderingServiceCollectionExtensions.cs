using Age.Core;
using Age.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Age.Rendering;

/// <summary>
/// Registers the rendering services of the engine.
/// </summary>
public static class RenderingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the window service, the renderer, the texture service, the splash screen, the render systems and the
    /// Silk.NET game loop as singletons.
    /// </summary>
    /// <remarks>
    /// <see cref="ITextureService"/> decodes through <see cref="T:Age.Assets.IImageLoader"/>, so register the asset
    /// services with <c>AddAgeAssets</c> as well.
    /// </remarks>
    public static IServiceCollection AddAgeRendering(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IWindowService, SilkWindowService>();
        services.AddSingleton<IRenderer, SilkRenderer>();
        services.AddSingleton<ITextureService, TextureService>();
        services.AddSingleton<SplashScreen>();
        services.AddSingleton<RenderSystem>();
        services.AddSingleton<UIRenderSystem>();
        services.AddSingleton<IGameLoop, SilkGameLoop>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="SilkInputService"/> as the input service, which reads the window instead of reporting nothing.
    /// </summary>
    /// <remarks>
    /// Call it after <c>AddAgeInput</c>, because the last registration of <see cref="IInputService"/> is the one that a
    /// single resolve returns. Use it only when the game runs with a window: the service opens the Silk.NET input
    /// context from that window on the first frame.
    /// </remarks>
    public static IServiceCollection AddAgeSilkInput(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IInputService, SilkInputService>();
        return services;
    }
}
