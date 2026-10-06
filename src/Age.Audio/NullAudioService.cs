namespace Age.Audio;

/// <summary>
/// An audio service that discards every request. It is intended for headless runs and tests.
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
