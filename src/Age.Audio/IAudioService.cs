namespace Age.Audio;

/// <summary>
/// Plays and stops sound effects. The default registration is <see cref="NullAudioService"/>, so register a real
/// implementation to hear anything.
/// </summary>
public interface IAudioService
{
    /// <summary>Plays the sound identified by <paramref name="soundId"/>.</summary>
    /// <param name="soundId">The identifier of a sound the implementation knows about. Sound resources are on the roadmap.</param>
    /// <param name="volume">The volume of this playback, where one is the recorded level and zero is silent.</param>
    /// <param name="loop">Whether the sound repeats until <see cref="StopAll"/> is called or the playback ends.</param>
    void PlaySound(int soundId, float volume = 1f, bool loop = false);

    /// <summary>Stops every sound that is currently playing.</summary>
    /// <remarks>Looping playbacks are stopped as well; there is no per-sound stop yet.</remarks>
    void StopAll();

    /// <summary>Sets the master volume, where one is the default level.</summary>
    /// <param name="volume">The master volume, applied on top of the volume of each playback.</param>
    void SetMasterVolume(float volume);
}
