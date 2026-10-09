using Age.Content.Locale;
using Age.Content.Prototypes;
using Age.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Age.Content;

/// <summary>
/// Registers the content services of the engine.
/// </summary>
public static class ContentServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="PrototypeManager"/>, which a game fills with the content of its own, the
    /// <see cref="SpawnService"/>, which makes entities out of it and answers a scene about its prototypes, and the
    /// <see cref="ILocaleService"/>, which answers with the strings of the language the game plays in.
    /// </summary>
    /// <remarks>
    /// Both are built over the <see cref="ComponentRegistry"/> of the container, so a game registers the kinds it reads,
    /// loads its content, and spawns what the content holds. Register the content <em>before</em> the first scene is
    /// loaded, because a scene that names a prototype needs the content that holds it. Call it after <c>AddAgeCore</c>,
    /// which registers that registry, and after <c>AddAgeAssets</c>, which the strings are read through.
    /// </remarks>
    public static IServiceCollection AddAgeContent(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(provider => new PrototypeManager(provider.GetRequiredService<ComponentRegistry>()));
        services.AddSingleton(provider => new SpawnService(provider.GetRequiredService<PrototypeManager>(), provider.GetRequiredService<ComponentRegistry>()));
        services.AddSingleton<IPrototypeSource>(provider => provider.GetRequiredService<SpawnService>());
        services.AddSingleton<ILocaleService>(provider => new LocaleService(
            provider.GetRequiredService<Age.Assets.IAssetLoader>(),
            provider.GetService<Microsoft.Extensions.Logging.ILogger<LocaleService>>()));
        return services;
    }
}
