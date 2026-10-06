namespace Age.Audio;

/// <summary>
/// The default <see cref="IAudioService"/>: a no-op that discards every request, so a game runs without an audio device.
/// Intended for headless runs and tests, and safe to leave registered when a game does not use sound.
/// </summary>
public sealed class NullAudioService : IAudioService
{
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
