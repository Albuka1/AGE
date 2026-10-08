using Age.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Age.Physics;

/// <summary>
/// Registers the physics services of the engine.
/// </summary>
public static class PhysicsServiceCollectionExtensions
{
    /// <summary>Registers <see cref="CollisionSystem"/> as a singleton and the physics components as part of a scene.</summary>
    public static IServiceCollection AddAgePhysics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<CollisionSystem>();
        services.AddSingleton<IComponentRegistrations, PhysicsComponentRegistrations>();
        return services;
    }
}
