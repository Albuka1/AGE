using Age.Content.Prototypes;
using Age.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Age.Content;

/// <summary>
/// Registers the content services of the engine.
/// </summary>
public static class ContentServiceCollectionExtensions
{
    /// <summary>Registers the <see cref="PrototypeManager"/>, which a game fills with the content of its own.</summary>
    /// <remarks>
    /// The manager is built over the <see cref="ComponentRegistry"/> of the container, so a game registers the kinds it
    /// reads and then loads its content. Call it after <c>AddAgeCore</c>, which registers that registry.
    /// </remarks>
    public static IServiceCollection AddAgeContent(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(provider => new PrototypeManager(provider.GetRequiredService<ComponentRegistry>()));
        return services;
    }
}
