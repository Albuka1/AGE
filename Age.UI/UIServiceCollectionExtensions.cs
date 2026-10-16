using Microsoft.Extensions.DependencyInjection;

namespace Age.UI;

/// <summary>
/// Registers the UI services of the engine.
/// </summary>
public static class UIServiceCollectionExtensions
{
    /// <summary>
    /// Registers the state of one frame, which the systems of the interface share and the layout system resets on every
    /// frame. The systems themselves are found by
    /// <see cref="Age.Core.SystemRegistrationExtensions.AddAgeSystems"/> when a host names this assembly, and the
    /// components by <see cref="Age.Core.SystemRegistrationExtensions.AddAgeComponentRegistrations"/> from the same call.
    /// </summary>
    /// <remarks>
    /// The state is a singleton because the systems that read the same pointer are: a click that one control took is a
    /// click the next one does not read, and the first of them resets the state on every frame.
    /// </remarks>
    public static IServiceCollection AddAgeUI(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<UIInputFrame>();
        return services;
    }
}