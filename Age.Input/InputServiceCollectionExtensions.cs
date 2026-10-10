using Microsoft.Extensions.DependencyInjection;

namespace Age.Input;

/// <summary>
/// Registers the input services of the engine.
/// </summary>
public static class InputServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IInputService"/>, <see cref="ITextInputService"/> and <see cref="IClipboardService"/> as
    /// singletons backed by the same <see cref="NullInputService"/>, which reports no key, no character and a clipboard of its own.
    /// </summary>
    /// <remarks>
    /// The three interfaces resolve to one instance, so a test that drives <see cref="NullInputService.Typed"/> drives what a
    /// console reads, and one that copies reads the same text back. Register <c>AddAgeSilkInput</c> after this call to read the
    /// window instead.
    /// </remarks>
    public static IServiceCollection AddAgeInput(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<NullInputService>();
        services.AddSingleton<IInputService>(provider => provider.GetRequiredService<NullInputService>());
        services.AddSingleton<ITextInputService>(provider => provider.GetRequiredService<NullInputService>());
        services.AddSingleton<IClipboardService>(provider => provider.GetRequiredService<NullInputService>());
        return services;
    }
}
