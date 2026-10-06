using Microsoft.Extensions.DependencyInjection;

namespace Age.UI;

/// <summary>
/// Registers the UI services of the engine.
/// </summary>
public static class UIServiceCollectionExtensions
{
    /// <summary>Registers <see cref="UIUpdateSystem"/> as a singleton.</summary>
    public static IServiceCollection AddAgeUI(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<UIUpdateSystem>();
        return services;
    }
}
