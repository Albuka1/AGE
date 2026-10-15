using Age.Assets;

namespace Age.Audio;

/// <summary>
/// The default <see cref="IAudioService"/>: a device that discards every request, so a game runs without an audio
/// device. Intended for headless runs and tests, and safe to leave registered when a game does not use sound.
/// </summary>
/// <remarks>
/// Uploading a sound reports zero, which is the identifier that <see cref="Play"/> ignores, so a game that loads and
/// plays sounds works unchanged on this device: it just stays silent. The channels answer one for their volume,
/// because a game that reads one back has to see the default it started with rather than a value that would make a
/// fade compute the wrong one.
/// </remarks>
public sealed class NullAudioService : IAudioService
{
    private readonly float[] _volumes = new float[Enum.GetValues<AudioChannel>().Length];

    /// <summary>Initializes a device whose channels all hold one.</summary>
    public NullAudioService()
    {
        for (int index = 0; index < _volumes.Length; index++)
        {
            _volumes[index] = 1f;
        }
    }

    /// <inheritdoc />
    public int PlayingCount => 0;

    /// <inheritdoc />
    /// <remarks>A device that discards every playback has no voice to run out of, so the value is kept and never used.</remarks>
    public int VoiceLimit { get; set; } = 32;

    /// <inheritdoc />
    public int CreateSound(SoundData sound) => 0;

    /// <inheritdoc />
    public void DeleteSound(int soundId)
    {
    }

    /// <inheritdoc />
    public PlaybackHandle Play(int soundId, in AudioPlaybackSettings settings) => PlaybackHandle.None;

    /// <inheritdoc />
    public void Stop(PlaybackHandle playback)
    {
    }

    /// <inheritdoc />
    public void Pause(PlaybackHandle playback)
    {
    }

    /// <inheritdoc />
    public void Resume(PlaybackHandle playback)
    {
    }

    /// <inheritdoc />
    public void StopAll()
    {
    }

    /// <inheritdoc />
    public void SetVolume(PlaybackHandle playback, float volume)
    {
    }

    /// <inheritdoc />
    public float GetVolume(PlaybackHandle playback) => 1f;

    /// <inheritdoc />
    public void SetPitch(PlaybackHandle playback, float pitch)
    {
    }

    /// <inheritdoc />
    public void SetPan(PlaybackHandle playback, float pan)
    {
    }

    /// <inheritdoc />
    public bool IsAlive(PlaybackHandle playback) => false;

    /// <inheritdoc />
    public void SetChannelVolume(AudioChannel channel, float volume) =>
        _volumes[(int)channel] = Math.Clamp(volume, 0f, 1f);

    /// <inheritdoc />
    public float GetChannelVolume(AudioChannel channel) => _volumes[(int)channel];
}