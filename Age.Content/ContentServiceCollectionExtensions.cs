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
    /// <see cref="SpawnService"/>, which makes entities out of it and answers a scene about its prototypes, the
    /// <see cref="ILocaleService"/>, which answers with the strings of the language the game plays in, and the
    /// <see cref="ITextSource"/> that answers the text of a component from it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both are built over the <see cref="ComponentRegistry"/> of the container, so a game registers the kinds it reads,
    /// loads its content, and spawns what the content holds. Register the content <em>before</em> the first scene is
    /// loaded, because a scene that names a prototype needs the content that holds it. Call it after <c>AddAgeCore</c>,
    /// which registers that registry, and after <c>AddAgeAssets</c>, which the strings are read through.
    /// </para>
    /// <para>
    /// The kinds of a content are not read here: <c>AddAgeContentKinds</c> of the assembly that declares them is what
    /// reads a class with <see cref="PrototypeKindAttribute"/>, because a kind is a word of a document and the assembly
    /// that says it is the host of a game rather than the engine of a content. The engine declares the first two kinds
    /// itself, and a game that names its own reads them from its own assembly with one call, which is why the tool that
    /// reads the content of the engine — <c>Age.Content.Lint</c> — names the two kinds it knows by hand.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddAgeContent(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The manager is registered once as the concrete type, because its constructor takes the registry and a game
        // fills it in a loading step. The interface is a factory over that registration, so a system that reads the
        // content and a host that loads it reach the same manager without the container holding two of them.
        services.AddSingleton(provider => new PrototypeManager(provider.GetRequiredService<ComponentRegistry>()));
        services.AddSingleton<IPrototypeManager>(provider => provider.GetRequiredService<PrototypeManager>());

        services.AddSingleton(provider => new SpawnService(provider.GetRequiredService<PrototypeManager>(), provider.GetRequiredService<ComponentRegistry>()));
        services.AddSingleton<IPrototypeSource>(provider => provider.GetRequiredService<SpawnService>());
        services.AddSingleton<ILocaleService>(provider => new LocaleService(
            provider.GetRequiredService<Age.Assets.IAssetLoader>(),
            provider.GetService<Microsoft.Extensions.Logging.ILogger<LocaleService>>()));
        services.AddSingleton<ITextSource>(provider => new LocaleTextSource(provider.GetRequiredService<ILocaleService>()));

        return services;
    }
}
