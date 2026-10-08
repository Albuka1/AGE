using Age.Assets;

namespace Age.Audio;

/// <summary>
/// Plays and stops sound effects. The default registration is <see cref="NullAudioService"/>, so register a real
/// implementation to hear anything.
/// </summary>
/// <remarks>
/// This is the device half of the audio subsystem: it holds sounds as buffers and mixes the playbacks that start from
/// them. <see cref="ISoundService"/> sits on top of it, decodes files and caches them by path.
/// </remarks>
public interface IAudioService
{
    /// <summary>Uploads decoded samples as a sound that the device can play.</summary>
    /// <param name="sound">The samples to upload. The device copies them, so the caller may reuse the buffer.</param>
    /// <returns>The identifier of the sound, which <see cref="PlaySound"/> and <see cref="DeleteSound"/> take. Zero means that the device holds no sound, which a device without output reports.</returns>
    int CreateSound(SoundData sound);

    /// <summary>Deletes a sound that <see cref="CreateSound"/> uploaded.</summary>
    /// <param name="soundId">The identifier of the sound to delete. An identifier without a sound is ignored.</param>
    void DeleteSound(int soundId);

    /// <summary>Plays a sound that <see cref="CreateSound"/> uploaded.</summary>
    /// <param name="soundId">The identifier of the sound to play. Zero plays nothing.</param>
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
