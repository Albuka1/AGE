using Microsoft.Extensions.DependencyInjection;

namespace Age.Audio;

/// <summary>
/// Registers the audio services of the engine.
/// </summary>
public static class AudioServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IAudioService"/> as a singleton backed by <see cref="NullAudioService"/>.</summary>
    public static IServiceCollection AddAgeAudio(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAudioService, NullAudioService>();
        return services;
    }
}
