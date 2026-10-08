using Microsoft.Extensions.DependencyInjection;

namespace Age.Input;

/// <summary>
/// Registers the input services of the engine.
/// </summary>
public static class InputServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IInputService"/> as a singleton backed by <see cref="NullInputService"/>.</summary>
    public static IServiceCollection AddAgeInput(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IInputService, NullInputService>();
        return services;
    }
}
