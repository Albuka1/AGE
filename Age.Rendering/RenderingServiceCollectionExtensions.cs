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
    /// pipeline, the render systems, the layout of the interface and the Silk.NET game loop as singletons.
    /// </summary>
    /// <remarks>
    /// <see cref="ITextureService"/> decodes through <see cref="T:Age.Assets.IImageLoader"/> and <see cref="IFontService"/>
    /// reads through <see cref="T:Age.Assets.IAssetLoader"/>, so register the asset services with <c>AddAgeAssets</c> as
    /// well. <see cref="SilkGameLoop"/> takes the <see cref="FixedTimestep"/> that <c>AddAgeCore</c> registers. The
    /// systems and the component registrations of this assembly are read by a scan here.
    /// </remarks>
    public static IServiceCollection AddAgeRendering(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IWindowService, SilkWindowService>();
        services.AddSingleton<IRenderer, SilkRenderer>();
        services.AddSingleton<ITextureService, TextureService>();
        services.AddSingleton<ISpriteSheetService, SpriteSheetService>();
        services.AddSingleton<IFontService, FontService>();
        services.AddSingleton<IShaderService, ShaderService>();
        services.AddSingleton<IMaterialService>(provider => new MaterialService(
            provider.GetRequiredService<IShaderService>(),
            provider.GetService<ILogger<MaterialService>>()));
        services.AddSingleton(provider => new TextRenderer(
            provider.GetRequiredService<IRenderer>(),
            provider.GetService<IFontService>(),
            provider.GetService<ITextSource>(),
            provider.GetService<ILogger<TextRenderer>>(),
            TextDefaults.Fonts));
        services.AddSingleton<SplashScreen>();
        services.AddSingleton<DevOverlay>();
        services.AddSingleton(provider => new ConsoleTab(
            provider.GetRequiredService<IConsoleService>(),
            provider.GetService<IClipboardService>()));

        services.AddSingleton<IDevWindowHost>(provider => new SilkDevWindowHost(provider.GetService<IWindowService>()));
        services.AddSingleton<IDevWindowService>(provider => new DevWindowService(
            provider.GetRequiredService<IDevWindowHost>())
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

        services.AddSingleton<RenderPipeline>();
        services.AddSingleton<RenderSystem>();
        services.AddSingleton<TextRenderSystem>();
        services.AddSingleton<UIRenderSystem>();
        services.AddSingleton<IGameLoop, SilkGameLoop>();

        services.AddAgeSystems(typeof(SilkRenderer).Assembly);
        services.AddAgeComponentRegistrations(typeof(SilkRenderer).Assembly);

        return services;
    }

    /// <summary>
    /// Registers <see cref="SilkInputService"/> as the input service, which reads the window instead of reporting nothing.
    /// </summary>
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