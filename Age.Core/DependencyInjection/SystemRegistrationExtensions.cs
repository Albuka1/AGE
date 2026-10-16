using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Age.Core;

/// <summary>
/// Registers the systems and the component registrations of an assembly, so that a game reads its own code rather than
/// listing it.
/// </summary>
/// <remarks>
/// The calls are the seam between what a class says about itself and what the container holds: a system is a class with
/// an attribute or an interface, and a registration is a class with an interface. Everything else about a game is what
/// the game registers by hand, because the order of a pipeline and the values of a factory are decisions of the game
/// rather than of a class.
/// </remarks>
public static class SystemRegistrationExtensions
{
    /// <summary>Registers every system of the assemblies that were named.</summary>
    /// <param name="services">The services to add the systems to.</param>
    /// <param name="assemblies">The assemblies to read, which is the assembly of a game and of every engine assembly it uses.</param>
    /// <returns>The services, so that several calls are one line.</returns>
    /// <exception cref="ArgumentNullException">The services or the assemblies are null.</exception>
    /// <remarks>
    /// <para>
    /// A class is a system when it carries <see cref="SystemAttribute"/> or when it implements an interface that carries
    /// <see cref="SystemInterfaceAttribute"/>. The class is registered under itself first, and every such interface is a
    /// factory over that registration, so a caller that asks for the interface and one that asks for the type reach the
    /// same object without the container holding two of it.
    /// </para>
    /// <para>
    /// A class that carries <see cref="ComponentRegistrationAttribute"/> is left to
    /// <see cref="AddAgeComponentRegistrations"/>: the registrations of an assembly are read by the same scan and by a
    /// different call, and registering one twice would hand the registry the same components a second time.
    /// </para>
    /// <para>
    /// An assembly that cannot be read whole is read as far as it can be: a type that failed to load is a type that no
    /// system of a game names, and reading the rest of them is what keeps a broken reference in one assembly from
    /// taking the systems of another with it.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddAgeSystems(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in Types(assembly))
            {
                if (!IsSystem(type, out SystemAttribute? attribute, out List<Type> interfaces))
                {
                    continue;
                }

                ServiceLifetime lifetime = attribute?.Lifetime ?? ServiceLifetime.Singleton;

                services.Add(new ServiceDescriptor(type, type, lifetime));

                foreach (Type iface in interfaces)
                {
                    Type captured = type;
                    services.Add(new ServiceDescriptor(iface, provider => provider.GetRequiredService(captured), lifetime));
                }
            }
        }

        return services;
    }

    /// <summary>Registers every set of component registrations of the assemblies that were named.</summary>
    /// <param name="services">The services to add the registrations to.</param>
    /// <param name="assemblies">The assemblies to read.</param>
    /// <returns>The services, so that several calls are one line.</returns>
    /// <exception cref="ArgumentNullException">The services or the assemblies are null.</exception>
    /// <remarks>
    /// A class that implements <see cref="IComponentRegistrations"/> is registered when it carries
    /// <see cref="ComponentRegistrationAttribute"/>, because the interface alone is what a test implements to pin a
    /// component down, and a scan has no way to tell the two apart.
    /// </remarks>
    public static IServiceCollection AddAgeComponentRegistrations(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in Types(assembly))
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                if (type.GetCustomAttribute<ComponentRegistrationAttribute>(inherit: false) is null)
                {
                    continue;
                }

                if (!typeof(IComponentRegistrations).IsAssignableFrom(type))
                {
                    continue;
                }

                services.AddSingleton(typeof(IComponentRegistrations), type);
            }
        }

        return services;
    }

    /// <summary>Returns a value indicating whether a type is a system, and what it registers under.</summary>
    /// <param name="type">The type to read.</param>
    /// <param name="attribute">Receives the attribute of the type, or null when it carries none.</param>
    /// <param name="interfaces">Receives the interfaces of the type that are marked with <see cref="SystemInterfaceAttribute"/>.</param>
    /// <returns><see langword="true"/> when the type is a system of one of the two kinds.</returns>
    private static bool IsSystem(Type type, out SystemAttribute? attribute, out List<Type> interfaces)
    {
        attribute = null;
        interfaces = [];

        if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
        {
            return false;
        }

        // A set of component registrations is read by a call of its own, because the interface that says so is what a
        // test implements to pin a component down, and the scan of the systems would find it as a system all the same.
        if (type.GetCustomAttribute<ComponentRegistrationAttribute>(inherit: false) is not null)
        {
            return false;
        }

        attribute = type.GetCustomAttribute<SystemAttribute>(inherit: false);

        foreach (Type iface in type.GetInterfaces())
        {
            if (iface.GetCustomAttribute<SystemInterfaceAttribute>(inherit: false) is not null)
            {
                interfaces.Add(iface);
            }
        }

        return attribute is not null || interfaces.Count > 0;
    }

    /// <summary>Returns the types of an assembly, which is what a scan walks.</summary>
    /// <param name="assembly">The assembly to read.</param>
    /// <returns>The types that the assembly holds, which are the ones it could load.</returns>
    /// <remarks>
    /// A type that failed to load is left out rather than thrown, because a scan of a whole assembly cannot refuse one
    /// class of it: the mistake belongs to whatever named the type, and the rest of the systems of the assembly are
    /// systems all the same.
    /// </remarks>
    private static IEnumerable<Type> Types(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null)!;
        }
    }
}