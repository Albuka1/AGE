using Microsoft.Extensions.DependencyInjection;

namespace Age.Input;

/// <summary>
/// Registers the input services of the engine.
/// </summary>
public static class InputServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IInputService"/> and <see cref="ITextInputService"/> as singletons backed by the same
    /// <see cref="NullInputService"/>, which reports no key and no character.
    /// </summary>
    /// <remarks>
    /// The two interfaces resolve to one instance, so a test that drives <see cref="NullInputService.Typed"/> drives what a
    /// console reads. Register <c>AddAgeSilkInput</c> after this call to read the window instead.
    /// </remarks>
    public static IServiceCollection AddAgeInput(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<NullInputService>();
        services.AddSingleton<IInputService>(provider => provider.GetRequiredService<NullInputService>());
        services.AddSingleton<ITextInputService>(provider => provider.GetRequiredService<NullInputService>());
        return services;
    }
}
