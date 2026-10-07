using Age.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Age.UI;

/// <summary>
/// Registers the UI services of the engine.
/// </summary>
public static class UIServiceCollectionExtensions
{
    /// <summary>Registers <see cref="UIUpdateSystem"/> as a singleton, together with the components of this assembly.</summary>
    /// <remarks>The components are registered so that a scene can hold them; the registry itself comes from <c>AddAgeCore</c>.</remarks>
    public static IServiceCollection AddAgeUI(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<UIUpdateSystem>();
        services.AddSingleton<IComponentRegistrations, UiComponentRegistrations>();
        return services;
    }
}
