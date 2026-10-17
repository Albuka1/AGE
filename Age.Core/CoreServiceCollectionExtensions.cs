using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Age.Core;

/// <summary>
/// Registers the core services of the engine.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the pipeline, the sorter, the fixed step, the registry of the components, the serializer of a scene,
    /// the console, the settings and the folders of a person, and the steps of a shutdown.
    /// </summary>
    /// <remarks>
    /// The systems and the component registrations of this assembly are read by a scan here, so a host that calls this
    /// one method reads the whole assembly rather than listing what it holds: a second call of the scan for the same
    /// assembly would register every system a second time. A logger is available without a logging builder: every
    /// service that asks for one gets <see cref="NullLogger{T}"/> until a game registers logging of its own, so logging
    /// is never required and never throws. Register that logging <em>before</em> this call, because the fallback is
    /// added only when nothing is there yet.
    /// </remarks>
    public static IServiceCollection AddAgeCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IConsoleService, ConsoleService>();

        // The data of a game belongs to the person playing it, so it lives under the roaming application data of the
        // account rather than beside the executable, which may be read-only. A game names itself by replacing this
        // registration with a UserDataService of its own, and the name of its entry assembly answers for a game that
        // does not.
        services.AddSingleton<IUserDataService>(_ => new UserDataService(Assembly.GetEntryAssembly()?.GetName().Name ?? "Age"));

        services.AddSingleton(new FixedTimestep(FixedTimestep.DefaultStep));
        services.AddSingleton<ComponentRegistry>(CreateComponentRegistry);

        // The serializer takes the content of the game when the container holds one, and a scene that was written from
        // entities of a prototype then reads back: AddAgeContent registers the source, and a game that never registers
        // it still reads a scene that carries every component of its entities.
        services.AddSingleton<ISceneSerializer>(provider => new SceneSerializer(
            provider.GetRequiredService<ComponentRegistry>(),
            provider.GetRequiredService<ILogger<SceneSerializer>>(),
            provider.GetService<IPrototypeSource>()));

        services.AddAgeSystems(typeof(World).Assembly);
        services.AddAgeComponentRegistrations(typeof(World).Assembly);

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
