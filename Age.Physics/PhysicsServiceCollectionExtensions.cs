using Age.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Age.Physics;

/// <summary>
/// Registers the physics services of the engine.
/// </summary>
public static class PhysicsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the collision system, which a game adds to its pipeline and the systems of which are found by
    /// <see cref="SystemRegistrationExtensions.AddAgeSystems"/> when a host names this assembly.
    /// </summary>
    /// <remarks>
    /// The components of this assembly are registered by
    /// <see cref="SystemRegistrationExtensions.AddAgeComponentRegistrations"/> from the same call. Neither is done
    /// here, because a scan of an assembly is a decision of the host of a game rather than of the assembly itself.
    /// </remarks>
    public static IServiceCollection AddAgePhysics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<CollisionSystem>();
        return services;
    }
}