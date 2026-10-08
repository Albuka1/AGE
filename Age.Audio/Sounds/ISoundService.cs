namespace Age.Audio;

/// <summary>
/// Loads sounds into the audio device and owns their lifetime. Sounds are cached by path, so loading the same file
/// twice returns the same sound instead of uploading it again.
/// </summary>
/// <example>
/// <code>
/// SoundHandle click = sounds.Load("Audio/Effects/click.wav");
/// sounds.Play(click);
///
/// sounds.Unload(click);
/// </code>
/// </example>
/// <remarks>
/// The service owns every sound it created, and it is not thread-safe: call it from the thread that owns the audio
/// device, which is the thread that runs the game loop.
/// </remarks>
public interface ISoundService
{
    /// <summary>Gets the number of sounds that are currently loaded.</summary>
    int Count { get; }

    /// <summary>Returns the sound of the file at the given path, decoding and uploading it on the first call.</summary>
    /// <param name="relativePath">The path of the sound, relative to the game root.</param>
    /// <returns>The sound of the file. Its <see cref="SoundHandle.Id"/> is zero when the device discarded it, which playing ignores.</returns>
    /// <exception cref="ArgumentException">The path is null, empty or whitespace.</exception>
    /// <exception cref="InvalidDataException">The file is not a sound in a supported format.</exception>
    /// <exception cref="FileNotFoundException">No file exists at that path.</exception>
    SoundHandle Load(string relativePath);

    /// <summary>Determines whether the handle still refers to a sound that this service loaded.</summary>
    /// <param name="sound">The handle to check.</param>
    /// <returns><see langword="true"/> while the sound is loaded. A handle the device made itself, and a handle from before an unload, both return <see langword="false"/>.</returns>
    bool IsAlive(SoundHandle sound);

    /// <summary>Plays a loaded sound through the device.</summary>
    /// <param name="sound">The sound to play.</param>
    /// <param name="volume">The volume of this playback, where one is the recorded level and zero is silent.</param>
    /// <param name="loop">Whether the sound repeats until <see cref="IAudioService.StopAll"/> is called.</param>
    /// <remarks>
    /// A handle that this service does not own, or one from before an unload, plays nothing. The playback mixes on top
    /// of the master volume that <see cref="IAudioService.SetMasterVolume"/> sets.
    /// </remarks>
    void Play(SoundHandle sound, float volume = 1f, bool loop = false);

    /// <summary>Deletes the sound behind the handle and forgets its path, so loading the path again decodes it anew.</summary>
    /// <param name="sound">The handle of the sound to delete.</param>
    /// <returns><see langword="true"/> when a loaded sound was deleted, <see langword="false"/> when the handle was stale or not owned by this service.</returns>
    bool Unload(SoundHandle sound);

    /// <summary>Deletes every sound that this service loaded.</summary>
    /// <remarks>
    /// A sound whose deletion the device refused stays loaded, so another call retries it, and every other sound is
    /// still deleted. The first refusal is reported, after the rest were deleted.
    /// </remarks>
    void UnloadAll();
}
