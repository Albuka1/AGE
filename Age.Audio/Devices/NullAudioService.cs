using Age.Assets;

namespace Age.Audio;

/// <summary>
/// The default <see cref="IAudioService"/>: a no-op that discards every request, so a game runs without an audio device.
/// Intended for headless runs and tests, and safe to leave registered when a game does not use sound.
/// </summary>
/// <remarks>
/// Uploading a sound reports zero, which is the identifier that <see cref="PlaySound"/> ignores, so a game that loads
/// and plays sounds works unchanged on this device: it just stays silent.
/// </remarks>
public sealed class NullAudioService : IAudioService
{
    /// <inheritdoc />
    public int CreateSound(SoundData sound) => 0;

    /// <inheritdoc />
    public void DeleteSound(int soundId)
    {
    }

    /// <inheritdoc />
    public void PlaySound(int soundId, float volume = 1f, bool loop = false)
    {
    }

    /// <inheritdoc />
    public void StopAll()
    {
    }

    /// <inheritdoc />
    public void SetMasterVolume(float volume)
    {
    }
}
