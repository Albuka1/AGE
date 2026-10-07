using Microsoft.Extensions.DependencyInjection;

namespace Age.Audio;

/// <summary>
/// Registers the audio services of the engine.
/// </summary>
public static class AudioServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAudioService"/> as a singleton backed by <see cref="NullAudioService"/> and
    /// <see cref="ISoundService"/> as a singleton backed by <see cref="SoundService"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="SoundService"/> decodes through <see cref="T:Age.Assets.ISoundLoader"/>, so register the asset
    /// services with <c>AddAgeAssets</c> as well. The null device keeps a game silent without an audio device; register
    /// a real one to hear anything.
    /// </remarks>
    public static IServiceCollection AddAgeAudio(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAudioService, NullAudioService>();
        services.AddSingleton<ISoundService, SoundService>();
        return services;
    }
}
