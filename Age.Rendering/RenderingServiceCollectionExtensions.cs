using Age.Core;
using Age.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Age.Rendering;

/// <summary>
/// Registers the rendering services of the engine.
/// </summary>
public static class RenderingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the window service, the renderer, the texture service, the font service, the splash screen, the render
    /// pipeline, the render systems, the layout of the interface, the components of this assembly and the Silk.NET game loop as
    /// singletons.
    /// </summary>
    /// <remarks>
    /// <see cref="ITextureService"/> decodes through <see cref="T:Age.Assets.IImageLoader"/> and <see cref="IFontService"/>
    /// reads through <see cref="T:Age.Assets.IAssetLoader"/>, so register the asset services with <c>AddAgeAssets</c> as
    /// well. <see cref="SilkGameLoop"/> takes the <see cref="FixedTimestep"/> that <c>AddAgeCore</c> registers.
    /// </remarks>
    public static IServiceCollection AddAgeRendering(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IWindowService, SilkWindowService>();
        services.AddSingleton<IRenderer, SilkRenderer>();
        services.AddSingleton<ITextureService, TextureService>();
        services.AddSingleton<ISpriteSheetService, SpriteSheetService>();
        services.AddSingleton<SpriteAnimationSystem>();
        services.AddSingleton<IFontService, FontService>();
        services.AddSingleton<IShaderService, ShaderService>();
        services.AddSingleton<IMaterialService>(provider => new MaterialService(
            provider.GetRequiredService<IShaderService>(),
            provider.GetService<ILogger<MaterialService>>()));
        services.AddSingleton(provider => new TextRenderer(
            provider.GetRequiredService<IRenderer>(),
            provider.GetService<IFontService>(),
            provider.GetService<ITextSource>(),
            provider.GetService<Microsoft.Extensions.Logging.ILogger<TextRenderer>>(),
            TextDefaults.Fonts));
        services.AddSingleton<SplashScreen>();
        services.AddSingleton<DevOverlay>();
        services.AddSingleton(provider => new ConsoleTab(
            provider.GetRequiredService<IConsoleService>(),
            provider.GetService<TextRenderer>()));
        services.AddSingleton(provider => new DevWindow(
            provider.GetRequiredService<IInputService>(),
            provider.GetRequiredService<IRenderer>(),
            provider.GetService<TextRenderer>(),
            provider.GetService<ITextSource>())
            .Add(provider.GetRequiredService<ConsoleTab>()));

        // The developer window that stands beside the game is a window of the operating system: its host creates the window and the
        // service draws the tabs into it. Register a host of your own — a null host leaves it with its pages and nothing drawn — to
        // change where it is drawn, and the service is what a game pumps each frame.
        services.AddSingleton<IDevWindowHost, SilkDevWindowHost>();
        services.AddSingleton<IDevWindowService>(provider => new DevWindowService(
            provider.GetRequiredService<IDevWindowHost>(),
            provider.GetService<ITextSource>())
            .Add(provider.GetRequiredService<ConsoleTab>()));
        services.AddSingleton(provider => new DevConsoleOverlay(
            provider.GetRequiredService<IConsoleService>(),
            provider.GetRequiredService<IInputService>(),
            provider.GetRequiredService<ITextInputService>(),
            provider.GetRequiredService<FixedTimestep>(),
            provider.GetRequiredService<IRenderer>(),
            provider.GetService<TextRenderer>(),
            provider.GetService<ITextSource>(),
            provider.GetService<IClipboardService>()));
        services.AddSingleton<IGameShutdownStep, RenderingShutdownStep>();
        services.AddSingleton<IGameShutdownStep, WindowShutdownStep>();
        services.AddSingleton<IComponentRegistrations, RenderingComponentRegistrations>();
        services.AddSingleton<RenderPipeline>();
        services.AddSingleton<RenderSystem>();
        services.AddSingleton<TextRenderSystem>();
        services.AddSingleton<UIRenderSystem>();
        services.AddSingleton<UILayoutSystem>();
        services.AddSingleton<IGameLoop, SilkGameLoop>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="SilkInputService"/> as the input service, which reads the window instead of reporting nothing.
    /// </summary>
    /// <remarks>
    /// Call it after <c>AddAgeInput</c>, because the last registration of <see cref="IInputService"/> is the one that a
    /// single resolve returns. The same service answers <see cref="ITextInputService"/>. Use it only when the game runs
    /// with a window: the service opens the Silk.NET input context from that window on the first frame.
    /// </remarks>
    public static IServiceCollection AddAgeSilkInput(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<SilkInputService>();
        services.AddSingleton<IInputService>(provider => provider.GetRequiredService<SilkInputService>());
        services.AddSingleton<ITextInputService>(provider => provider.GetRequiredService<SilkInputService>());
        services.AddSingleton<IClipboardService>(provider => provider.GetRequiredService<SilkInputService>());
        return services;
    }
}
