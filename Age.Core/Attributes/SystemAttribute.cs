using Microsoft.Extensions.DependencyInjection;

namespace Age.Core;

/// <summary>
/// Marks a class as a system that the container registers on its own, which is what keeps a game from listing every
/// system it holds in one place.
/// </summary>
/// <remarks>
/// <para>
/// A system is registered under itself and under every interface of it that is marked with
/// <see cref="SystemInterfaceAttribute"/>, so a caller that wants the concrete type and one that wants the interface
/// reach the same object. A class that carries no attribute and answers one system interface is reached through that
/// interface alone, because a second registration of the same type is a second way to say the same thing.
/// </para>
/// <para>
/// The attribute does not decide the order of the systems in a pipeline: a pipeline is built by hand, because the order
/// of a step is a decision of a game rather than of a class.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SystemAttribute : Attribute
{
    /// <summary>Initializes the attribute with the lifetime the system is registered with.</summary>
    /// <param name="lifetime">The lifetime of the registration, which is a singleton when a game does not say.</param>
    public SystemAttribute(ServiceLifetime lifetime = ServiceLifetime.Singleton) => Lifetime = lifetime;

    /// <summary>Gets the lifetime that the system is registered with.</summary>
    public ServiceLifetime Lifetime { get; }
}
