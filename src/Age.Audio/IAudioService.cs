namespace Age.Audio;

/// <summary>
/// Plays and stops sound effects.
/// </summary>
public interface IAudioService
{
    /// <summary>Plays the sound identified by <paramref name="soundId"/>.</summary>
    void PlaySound(int soundId, float volume = 1f, bool loop = false);

    /// <summary>Stops every sound that is currently playing.</summary>
    void StopAll();

    /// <summary>Sets the master volume, where one is the default level.</summary>
    void SetMasterVolume(float volume);
}
