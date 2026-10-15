using Age.Assets;

namespace Age.Audio;

/// <summary>
/// Plays, pauses and mixes the sounds of a game through the channels of the device. The default registration is
/// <see cref="NullAudioService"/>, so register a real one to hear anything.
/// </summary>
/// <remarks>
/// <para>
/// This is the device half of the audio subsystem: it holds sounds as buffers, mixes the playbacks that start from
/// them, and answers for the state of one that a game holds. <see cref="ISoundService"/> sits on top of it, decodes
/// the files of a game and hands out a <see cref="SoundHandle"/> for a sound by its path.
/// </para>
/// <para>
/// A playback is heard in a channel, which carries a volume of its own, and every channel is under a master that
/// multiplies the whole mix. What a game changes while a sound plays — the volume of a fade, the pitch of a doppler,
/// the pan of a moving source — goes through the handle that the play returned.
/// </para>
/// </remarks>
public interface IAudioService
{
    /// <summary>Gets the number of playbacks the device is running at this moment, whether they play or are paused.</summary>
    /// <remarks>A device that discards sounds answers zero, which is what a game reads to know that nothing is heard.</remarks>
    int PlayingCount { get; }

    /// <summary>Gets or sets the number of voices the device runs at the same time.</summary>
    /// <remarks>
    /// A playback that starts when every voice is busy takes the voice of the playback of the lowest priority, and a
    /// playback that no running one is of a lower priority than is dropped. A device without a limit — which is what
    /// the null device is — ignores the value.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    int VoiceLimit { get; set; }

    /// <summary>Uploads decoded samples as a sound that the device can play.</summary>
    /// <param name="sound">The samples to upload. The device copies them, so the caller may reuse the buffer.</param>
    /// <returns>The identifier of the sound, which <see cref="Play"/> and <see cref="DeleteSound"/> take. Zero means that the device holds no sound, which a device without output reports.</returns>
    int CreateSound(SoundData sound);

    /// <summary>Deletes a sound that <see cref="CreateSound"/> uploaded, and stops every playback of it.</summary>
    /// <param name="soundId">The identifier of the sound to delete. An identifier without a sound is ignored.</param>
    void DeleteSound(int soundId);

    /// <summary>Starts a playback of a sound and answers the handle that names it.</summary>
    /// <param name="soundId">The identifier of the sound to play. Zero plays nothing and answers <see cref="PlaybackHandle.None"/>.</param>
    /// <param name="settings">How the playback is heard.</param>
    /// <returns>The handle of the playback, or <see cref="PlaybackHandle.None"/> when the device discarded it or the sound is not one of this device.</returns>
    PlaybackHandle Play(int soundId, in AudioPlaybackSettings settings);

    /// <summary>Stops a playback and answers the voice it was using to the device.</summary>
    /// <param name="playback">The handle of the playback. A handle the device does not hold is ignored.</param>
    void Stop(PlaybackHandle playback);

    /// <summary>Pauses a playback without answering its voice back to the device.</summary>
    /// <param name="playback">The handle of the playback.</param>
    /// <remarks>A paused playback keeps the voice of the device, so a game that pauses a long sound keeps a voice for it rather than starting it over later.</remarks>
    void Pause(PlaybackHandle playback);

    /// <summary>Resumes a playback that was paused.</summary>
    /// <param name="playback">The handle of the playback.</param>
    void Resume(PlaybackHandle playback);

    /// <summary>Stops every playback, in every channel.</summary>
    void StopAll();

    /// <summary>Sets the volume of one playback, on top of the volume of its channel.</summary>
    /// <param name="playback">The handle of the playback.</param>
    /// <param name="volume">The volume of the playback, where one is the recorded level and zero is silent.</param>
    void SetVolume(PlaybackHandle playback, float volume);

    /// <summary>Returns the volume of one playback, which is what a fade reads back before it starts.</summary>
    /// <param name="playback">The handle of the playback.</param>
    /// <returns>The volume of the playback, or one for a handle the device does not hold.</returns>
    float GetVolume(PlaybackHandle playback);

    /// <summary>Sets the speed of one playback, where one is the recorded speed.</summary>
    /// <param name="playback">The handle of the playback.</param>
    /// <param name="pitch">The speed of the playback, which is clamped to a tenth at the lowest.</param>
    void SetPitch(PlaybackHandle playback, float pitch);

    /// <summary>Sets where one playback stands between the two speakers.</summary>
    /// <param name="playback">The handle of the playback.</param>
    /// <param name="pan">Minus one is the left speaker, zero the middle and one the right speaker.</param>
    void SetPan(PlaybackHandle playback, float pan);

    /// <summary>Determines whether the device still holds a playback under a handle, whether it plays or is paused.</summary>
    /// <param name="playback">The handle of the playback.</param>
    /// <returns><see langword="true"/> while the device holds the voice, and <see langword="false"/> for a default handle, one whose voice was answered back, and one of a device the handle does not belong to.</returns>
    bool IsAlive(PlaybackHandle playback);

    /// <summary>Sets the volume of one channel, which every playback of it is multiplied by.</summary>
    /// <param name="channel">The channel to write.</param>
    /// <param name="volume">The volume of the channel, where one is the recorded level and zero is silent.</param>
    void SetChannelVolume(AudioChannel channel, float volume);

    /// <summary>Returns the volume of one channel, which is what a settings menu reads back.</summary>
    /// <param name="channel">The channel to read.</param>
    /// <returns>The volume of the channel. A device that mixes nothing answers one.</returns>
    float GetChannelVolume(AudioChannel channel);
}
