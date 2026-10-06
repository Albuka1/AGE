using Microsoft.Extensions.DependencyInjection;

namespace Age.Core;

/// <summary>
/// Registers the core services of the engine.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>Registers <see cref="SystemPipeline"/> and <see cref="SpriteSorter"/> as singletons.</summary>
    public static IServiceCollection AddAgeCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<SystemPipeline>();
        services.AddSingleton<SpriteSorter>();
        return services;
    }
}
