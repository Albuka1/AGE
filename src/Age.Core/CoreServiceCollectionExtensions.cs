using Microsoft.Extensions.DependencyInjection;

namespace Age.Core;

/// <summary>
/// Registers the core services of the engine.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SystemPipeline"/>, <see cref="SpriteSorter"/>, the <see cref="ComponentRegistry"/> that
    /// collects the components of every registered assembly, and <see cref="ISceneSerializer"/>.
    /// </summary>
    /// <remarks>
    /// Every assembly adds its own <see cref="IComponentRegistrations"/> when its services are registered, so the
    /// registry holds every component whatever the order of the calls. Register a game's own implementation to make
    /// its components part of a scene.
    /// </remarks>
    public static IServiceCollection AddAgeCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<SystemPipeline>();
        services.AddSingleton<SpriteSorter>();
        services.AddSingleton<IComponentRegistrations, CoreComponentRegistrations>();
        services.AddSingleton<ComponentRegistry>(CreateComponentRegistry);
        services.AddSingleton<ISceneSerializer, SceneSerializer>();
        return services;
    }

    /// <summary>Builds the registry from every registration in the container.</summary>
    private static ComponentRegistry CreateComponentRegistry(IServiceProvider provider)
    {
        var registry = new ComponentRegistry();

        foreach (IComponentRegistrations registrations in provider.GetServices<IComponentRegistrations>())
        {
            registrations.Register(registry);
        }

        return registry;
    }
}
