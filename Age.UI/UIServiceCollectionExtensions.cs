using Age.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Age.UI;

/// <summary>
/// Registers the UI services of the engine.
/// </summary>
public static class UIServiceCollectionExtensions
{
    /// <summary>Registers the UI systems as singletons, together with the components of this assembly.</summary>
    /// <remarks>The components are registered so that a scene can hold them; the registry itself comes from <c>AddAgeCore</c>.</remarks>
    public static IServiceCollection AddAgeUI(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The state of one frame is shared by the systems that read the same pointer, so a click that one control took is a click the
        // next one does not read: it is a singleton because the systems are, and it is reset by the first of them on every frame.
        services.AddSingleton<UIInputFrame>();
        services.AddSingleton<UIUpdateSystem>();
        services.AddSingleton<TextInputSystem>();
        services.AddSingleton<IComponentRegistrations, UiComponentRegistrations>();
        return services;
    }
}
