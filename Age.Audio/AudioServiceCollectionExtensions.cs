using Age.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Age.Audio;

/// <summary>
/// Registers the audio services of the engine.
/// </summary>
public static class AudioServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="OpenALAudioService"/> as the audio device, which plays sounds instead of discarding them.
    /// </summary>
    /// <remarks>
    /// Call it after <c>AddAgeAudio</c>, because the last registration of <see cref="IAudioService"/> is the one that a
    /// single resolve returns. Use it only where an audio device exists: the service fails to construct without one,
    /// which is why the null device stays the default.
    /// </remarks>
    public static IServiceCollection AddAgeOpenALAudio(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAudioService, OpenALAudioService>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="IAudioService"/> as a singleton backed by <see cref="NullAudioService"/>,
    /// <see cref="ISoundService"/> as a singleton backed by <see cref="SoundService"/>, and the fades of a game as
    /// <see cref="IAudioFader"/>, backed by <see cref="AudioFader"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="SoundService"/> decodes through <see cref="T:Age.Assets.ISoundLoader"/>, so register the asset
    /// services with <c>AddAgeAssets</c> as well. The null device keeps a game silent without an audio device;
    /// register a real one to hear anything.
    /// </para>
    /// <para>
    /// <see cref="AudioFader"/> is a frame system, so a game that fades a channel or a playback adds it to its pipeline
    /// with <c>AddFrame</c> itself: it is registered here so a game resolves the one instance the device and the
    /// pipeline both use, and a game that never calls <c>Fade</c> never has to add it.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddAgeAudio(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IAudioService, NullAudioService>();
        services.AddSingleton<ISoundService, SoundService>();
        services.AddSingleton<AudioFader>();
        services.AddSingleton<IAudioFader>(provider => provider.GetRequiredService<AudioFader>());
        services.AddSingleton<IGameShutdownStep, AudioShutdownStep>();
        return services;
    }
}
