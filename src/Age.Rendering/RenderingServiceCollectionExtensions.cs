using Age.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Age.Rendering;

/// <summary>
/// Registers the rendering services of the engine.
/// </summary>
public static class RenderingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the window service, the renderer, the render systems and the Silk.NET game loop as singletons.
    /// </summary>
    public static IServiceCollection AddAgeRendering(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IWindowService, SilkWindowService>();
        services.AddSingleton<IRenderer, SilkRenderer>();
        services.AddSingleton<RenderSystem>();
        services.AddSingleton<UIRenderSystem>();
        services.AddSingleton<IGameLoop, SilkGameLoop>();
        return services;
    }
}
