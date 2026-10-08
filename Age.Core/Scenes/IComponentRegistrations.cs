namespace Age.Core;

/// <summary>
/// Contributes component types to the <see cref="ComponentRegistry"/> that the scene serializer uses.
/// </summary>
/// <remarks>
/// Every assembly of the engine registers its own components through an implementation of this interface, and a game
/// does the same for the components it adds: <c>AddAgeCore</c> collects every implementation that is in the container,
/// whatever the order of the registrations.
/// </remarks>
public interface IComponentRegistrations
{
    /// <summary>Registers the components of one assembly.</summary>
    /// <param name="registry">The registry to add the components to.</param>
    void Register(ComponentRegistry registry);
}
