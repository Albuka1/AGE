using Age.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Age.Physics;

/// <summary>
/// Registers the physics services of the engine.
/// </summary>
public static class PhysicsServiceCollectionExtensions
{
    /// <summary>Registers the systems and the component registrations of this assembly, which is what a host reads.</summary>
    public static IServiceCollection AddAgePhysics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddAgeSystems(typeof(CollisionSystem).Assembly);
        services.AddAgeComponentRegistrations(typeof(CollisionSystem).Assembly);
        return services;
    }
}
