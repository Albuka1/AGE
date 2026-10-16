using Age.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Age.UI;

/// <summary>
/// Registers the UI services of the engine.
/// </summary>
public static class UIServiceCollectionExtensions
{
    /// <summary>
    /// Registers the state of one frame, which the systems of the interface share and the layout system resets on every
    /// frame, and the systems and the component registrations of this assembly.
    /// </summary>
    /// <remarks>
    /// The state is a singleton because the systems that read the same pointer are: a click that one control took is a
    /// click the next one does not read, and the first of them resets the state on every frame.
    /// </remarks>
    public static IServiceCollection AddAgeUI(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<UIInputFrame>();
        services.AddAgeSystems(typeof(UIInputFrame).Assembly);
        services.AddAgeComponentRegistrations(typeof(UIInputFrame).Assembly);
        return services;
    }
}
