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
    /// Registers <see cref="SystemPipeline"/>, <see cref="SpriteSorter"/>, the <see cref="FixedTimestep"/> of the game
    /// loops, the <see cref="ComponentRegistry"/> that collects the components of every registered assembly,
    /// <see cref="ISceneSerializer"/>, the <see cref="IConsoleService"/>, the <see cref="CVarService"/> and the
    /// <see cref="IUserDataService"/> of a developer, and <see cref="GameShutdown"/>.
    /// </summary>
    /// <remarks>
    /// Every assembly adds its own <see cref="IComponentRegistrations"/> when its services are registered, so the
    /// registry holds every component whatever the order of the calls. Register a game's own implementation to make
    /// its components part of a scene. The fixed step is one sixtieth of a second; register a <see cref="FixedTimestep"/>
    /// of your own after this call to change it, because the last registration wins. The <see cref="TimerSystem"/> and
    /// the <see cref="TweenSystem"/> are services like any other, so a game adds them to its pipeline by resolving them.
    /// A logger is available without a logging builder: every service that asks for one gets
    /// <see cref="NullLogger{T}"/> until a game registers logging of its own, so logging is never required and never
    /// throws. Register that logging <em>before</em> this call, because the fallback is added only when nothing is there
    /// yet.
    /// </remarks>
    public static IServiceCollection AddAgeCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<SystemPipeline>();
        services.AddSingleton<SpriteSorter>();
        services.AddSingleton<TimerSystem>();
        services.AddSingleton<TweenSystem>();
        services.AddSingleton<GameShutdown>();
        services.AddSingleton<IConsoleService, ConsoleService>();
        services.AddSingleton<CVarService>();

        // The data of a game belongs to the person playing it, so it lives under the roaming application data of the account
        // rather than beside the executable, which may be read-only. A game names itself by replacing this registration with a
        // UserDataService of its own, and the name of its entry assembly answers for a game that does not.
        services.AddSingleton<IUserDataService>(_ => new UserDataService(Assembly.GetEntryAssembly()?.GetName().Name ?? "Age"));

        services.AddSingleton(new FixedTimestep(FixedTimestep.DefaultStep));
        services.AddSingleton<IComponentRegistrations, CoreComponentRegistrations>();
        services.AddSingleton<ComponentRegistry>(CreateComponentRegistry);

        // The serializer takes the content of the game when the container holds one, and a scene that was written from
        // entities of a prototype then reads back: AddAgeContent registers the source, and a game that never registers it
        // still reads a scene that carries every component of its entities.
        services.AddSingleton<ISceneSerializer>(provider => new SceneSerializer(
            provider.GetRequiredService<ComponentRegistry>(),
            provider.GetRequiredService<ILogger<SceneSerializer>>(),
            provider.GetService<IPrototypeSource>()));
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
