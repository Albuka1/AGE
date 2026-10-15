using Age.Assets;
using Age.Audio;
using Age.Core;

namespace Age.Tests;

/// <summary>
/// The audio device of the tests: a pool of voices that records what it was asked to do and answers the volume of
/// a channel and of a playback from what the calls left behind.
/// </summary>
/// <remarks>
/// It plays the role of <see cref="IAudioService"/> the way a real device does: a play returns a handle that names a
/// voice, a stop returns the voice to the pool, and the two questions that a fader asks — whether a voice is still
/// there and what its volume is — answer what the calls wrote. It plays nothing, because a test does not listen to
/// sound: it reads what the calls were.
/// </remarks>
internal sealed class FakeAudioService : IAudioService
{
    private readonly ResourcePool<int, Voice> _voices = new();
    private readonly float[] _channels = new float[Enum.GetValues<AudioChannel>().Length];

    public FakeAudioService()
    {
        // The channels of a device all hold the recorded level, which is what the mixer of a game starts from.
        for (int index = 0; index < _channels.Length; index++)
        {
            _channels[index] = 1f;
        }
    }

    /// <summary>Gets the identifiers of the sounds the device uploaded, in the order the calls asked for them.</summary>
    public List<int> Created { get; } = [];

    /// <summary>Gets the identifiers of the sounds the device deleted, in the order the calls asked for them.</summary>
    public List<int> Deleted { get; } = [];

    /// <summary>Gets what every play of the device was asked for, in the order the calls asked for it.</summary>
    public List<(int SoundId, AudioPlaybackSettings Settings)> Played { get; } = [];

    /// <summary>Gets the identifiers of the sounds whose voices the device stopped.</summary>
    public List<int> Stopped { get; } = [];

    /// <summary>Gets the identifiers of the sounds whose voices the device paused.</summary>
    public List<int> Paused { get; } = [];

    /// <summary>Gets the identifiers of the sounds whose voices the device resumed.</summary>
    public List<int> Resumed { get; } = [];

    /// <summary>Gets what every write of a playback volume was, in the order the calls asked for it.</summary>
    public List<(PlaybackHandle Playback, float Volume)> VolumeWrites { get; } = [];

    /// <summary>Gets what every write of a channel volume was, in the order the calls asked for it.</summary>
    public List<(AudioChannel Channel, float Volume)> ChannelWrites { get; } = [];

    /// <summary>Gets the samples of the last sound the device uploaded.</summary>
    public SoundData? LastSound { get; private set; }

    /// <summary>Gets or sets a test that decides whether the deletion of one sound fails, which is what the retry of a shutdown is checked with.</summary>
    public Func<int, bool>? FailDelete { get; set; }

    /// <inheritdoc />
    public int PlayingCount => _voices.Count;

    /// <inheritdoc />
    public int VoiceLimit { get; set; } = 32;

    /// <inheritdoc />
    public int CreateSound(SoundData sound)
    {
        LastSound = sound;
        int id = Created.Count + 1;
        Created.Add(id);
        return id;
    }

    /// <inheritdoc />
    public void DeleteSound(int soundId)
    {
        Deleted.Add(soundId);

        if (FailDelete?.Invoke(soundId) == true)
        {
            throw new InvalidOperationException($"The device refused to delete sound {soundId}.");
        }
    }

    /// <inheritdoc />
    public PlaybackHandle Play(int soundId, in AudioPlaybackSettings settings)
    {
        var voice = new Voice(soundId, settings);
        ResourceHandle slot = _voices.Add(voice);
        Played.Add((soundId, settings));
        return new PlaybackHandle(slot);
    }

    /// <inheritdoc />
    public void Stop(PlaybackHandle playback)
    {
        if (_voices.TryGet(playback.Voice, out Voice? voice) && voice is not null)
        {
            Stopped.Add(voice.SoundId);
            _voices.Release(playback.Voice);
        }
    }

    /// <inheritdoc />
    public void Pause(PlaybackHandle playback)
    {
        if (_voices.TryGet(playback.Voice, out Voice? voice) && voice is not null)
        {
            Paused.Add(voice.SoundId);
        }
    }

    /// <inheritdoc />
    public void Resume(PlaybackHandle playback)
    {
        if (_voices.TryGet(playback.Voice, out Voice? voice) && voice is not null)
        {
            Resumed.Add(voice.SoundId);
        }
    }

    /// <inheritdoc />
    public void StopAll()
    {
        foreach (ResourceHandle slot in _voices.GetHandles())
        {
            if (_voices.TryGet(slot, out Voice? voice) && voice is not null)
            {
                Stopped.Add(voice.SoundId);
                _voices.Release(slot);
            }
        }
    }

    /// <inheritdoc />
    public void SetVolume(PlaybackHandle playback, float volume)
    {
        VolumeWrites.Add((playback, volume));

        if (_voices.TryGet(playback.Voice, out Voice? voice) && voice is not null)
        {
            voice.Volume = volume;
        }
    }

    /// <inheritdoc />
    public float GetVolume(PlaybackHandle playback) =>
        _voices.TryGet(playback.Voice, out Voice? voice) && voice is not null ? voice.Volume : 1f;

    /// <inheritdoc />
    public void SetPitch(PlaybackHandle playback, float pitch)
    {
        if (_voices.TryGet(playback.Voice, out Voice? voice) && voice is not null)
        {
            voice.Pitch = pitch;
        }
    }

    /// <inheritdoc />
    public void SetPan(PlaybackHandle playback, float pan)
    {
        if (_voices.TryGet(playback.Voice, out Voice? voice) && voice is not null)
        {
            voice.Pan = pan;
        }
    }

    /// <inheritdoc />
    public bool IsAlive(PlaybackHandle playback) =>
        playback.Voice.IsValid && _voices.TryGet(playback.Voice, out _);

    /// <inheritdoc />
    public void SetChannelVolume(AudioChannel channel, float volume)
    {
        ChannelWrites.Add((channel, volume));
        _channels[(int)channel] = Math.Clamp(volume, 0f, 1f);
    }

    /// <inheritdoc />
    public float GetChannelVolume(AudioChannel channel) => _channels[(int)channel];

    /// <summary>One voice of the device: the sound it plays and what a game wrote to it.</summary>
    private sealed class Voice
    {
        public Voice(int soundId, in AudioPlaybackSettings settings)
        {
            SoundId = soundId;
            Channel = settings.Channel;
            Priority = settings.Priority;
            Volume = settings.Volume;
            Pitch = settings.Pitch;
            Pan = settings.Pan;
        }

        public int SoundId { get; }
        public AudioChannel Channel { get; }
        public int Priority { get; }
        public float Volume { get; set; }
        public float Pitch { get; set; }
        public float Pan { get; set; }
    }
}