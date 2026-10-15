using Age.Assets;
using Age.Core;
using Silk.NET.OpenAL;

namespace Age.Audio;

/// <summary>
/// An <see cref="IAudioService"/> backed by OpenAL through Silk.NET. It opens the default device when it is created,
/// holds every sound in an OpenAL buffer, and plays them from a pool of voices.
/// </summary>
/// <remarks>
/// <para>
/// This service is not registered by <c>AddAgeAudio</c>, because a machine without audio would fail to start: register
/// it with <c>AddAgeOpenALAudio</c> when the game plays sound and leave the null device in place for headless runs.
/// </para>
/// <para>
/// The voices are a pool of a size that a game sets with <see cref="VoiceLimit"/>: a playback that starts when every
/// voice is busy takes the voice of the playback of the lowest priority, and one that no running playback is of a lower
/// priority than is dropped. That is what keeps the number of OpenAL sources under the limit the device reports rather
/// than growing with every sound the game plays.
/// </para>
/// </remarks>
public sealed unsafe class OpenALAudioService : IAudioService, IDisposable
{
    private const int DefaultVoiceLimit = 32;

    private readonly ALContext _alc;
    private readonly AL _al;
    private readonly Device* _device;
    private readonly Context* _context;
    private readonly ResourcePool<int, Voice> _voices = new();
    private readonly float[] _channelVolumes = new float[Enum.GetValues<AudioChannel>().Length];
    private int _voiceLimit = DefaultVoiceLimit;
    private bool _disposed;

    /// <summary>Opens the default audio device and makes its context current.</summary>
    /// <exception cref="InvalidOperationException">No audio device is available, or OpenAL is not installed.</exception>
    public OpenALAudioService()
    {
        try
        {
            _alc = ALContext.GetApi();
            _device = _alc.OpenDevice(null);

            if (_device is null)
            {
                _alc.Dispose();
                throw new InvalidOperationException("No audio device is available. Register the null device instead of this one to run without sound.");
            }

            _context = _alc.CreateContext(_device, null);
            _alc.MakeContextCurrent(_context);
            _al = AL.GetApi();
        }
        catch (DllNotFoundException exception)
        {
            throw new InvalidOperationException("OpenAL is not installed, so the audio device cannot be opened. Register the null device instead of this one to run without sound.", exception);
        }

        for (int index = 0; index < _channelVolumes.Length; index++)
        {
            _channelVolumes[index] = 1f;
        }

        // The listener of a game of two dimensions stands at the origin of the world of the game and never moves: a
        // source is heard from where its pan puts it and not from where it stands, which is what makes a pan of minus
        // one a sound against the left ear rather than a sound that is somewhere to the left of the listener.
        _al.SetListenerProperty(ListenerVector3.Position, 0f, 0f, 0f);
        _al.SetListenerProperty(ListenerVector3.Velocity, 0f, 0f, 0f);
    }

    /// <inheritdoc />
    public int PlayingCount => _voices.Count;

    /// <inheritdoc />
    public int VoiceLimit
    {
        get => _voiceLimit;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _voiceLimit = value;
            ReleaseOverLimit();
        }
    }

    /// <inheritdoc />
    public int CreateSound(SoundData sound)
    {
        ArgumentNullException.ThrowIfNull(sound);

        _ = _al.GetError();

        uint buffer = _al.GenBuffer();
        _al.BufferData(buffer, sound.Channels == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16, sound.Samples, sound.SampleRate);

        AudioError error = _al.GetError();
        if (error != AudioError.NoError)
        {
            _al.DeleteBuffer(buffer);
            _ = _al.GetError();
            throw new InvalidOperationException($"The audio device refused the sound: {error}.");
        }

        return (int)buffer;
    }

    /// <inheritdoc />
    public void DeleteSound(int soundId)
    {
        if (soundId == 0)
        {
            return;
        }

        // A voice keeps the buffer it plays alive, so a sound that is deleted while it plays is stopped first: a buffer
        // that is deleted under a source that plays it is a mistake of the device rather than of the game.
        foreach (ResourceHandle slot in _voices.GetHandles())
        {
            if (_voices.TryGet(slot, out Voice? voice) && voice is not null && voice.SoundId == soundId)
            {
                Release(slot, voice);
            }
        }

        _ = _al.GetError();
        _al.DeleteBuffer((uint)soundId);

        AudioError error = _al.GetError();
        if (error != AudioError.NoError)
        {
            throw new InvalidOperationException($"The audio device refused to delete sound {soundId}: {error}.");
        }
    }

    /// <inheritdoc />
    public PlaybackHandle Play(int soundId, in AudioPlaybackSettings settings)
    {
        if (soundId == 0)
        {
            return PlaybackHandle.None;
        }

        ReleaseFinished();

        Voice? voice = TakeVoice(settings.Priority);

        if (voice is null)
        {
            return PlaybackHandle.None;
        }

        voice.SoundId = soundId;
        voice.Channel = settings.Channel;
        voice.Priority = settings.Priority;
        voice.Volume = settings.Volume;
        voice.Pitch = settings.Pitch;
        voice.Pan = settings.Pan;

        _al.SetSourceProperty(voice.Source, SourceInteger.Buffer, (uint)soundId);
        _al.SetSourceProperty(voice.Source, SourceFloat.Gain, GainOf(settings.Channel, settings.Volume));
        _al.SetSourceProperty(voice.Source, SourceFloat.Pitch, MathF.Max(settings.Pitch, 0.1f));
        _al.SetSourceProperty(voice.Source, SourceBoolean.Looping, settings.Loop);
        _al.SetSourceProperty(voice.Source, SourceVector3.Position, Math.Clamp(settings.Pan, -1f, 1f), 0f, 0f);

        _al.SourcePlay(voice.Source);
        return new PlaybackHandle(voice.Resource);
    }

    /// <inheritdoc />
    public void Stop(PlaybackHandle playback)
    {
        if (_voices.TryGet(playback.Voice, out Voice? voice) && voice is not null)
        {
            Release(playback.Voice, voice);
        }
    }

    /// <inheritdoc />
    public void Pause(PlaybackHandle playback)
    {
        if (_voices.TryGet(playback.Voice, out Voice? voice) && voice is not null)
        {
            _al.SourcePause(voice.Source);
        }
    }

    /// <inheritdoc />
    public void Resume(PlaybackHandle playback)
    {
        if (_voices.TryGet(playback.Voice, out Voice? voice) && voice is not null)
        {
            _al.SourcePlay(voice.Source);
        }
    }

    /// <inheritdoc />
    public void StopAll()
    {
        foreach (ResourceHandle slot in _voices.GetHandles())
        {
            if (_voices.TryGet(slot, out Voice? voice) && voice is not null)
            {
                Release(slot, voice);
            }
        }
    }

    /// <inheritdoc />
    public void SetVolume(PlaybackHandle playback, float volume)
    {
        if (!_voices.TryGet(playback.Voice, out Voice? voice) || voice is null)
        {
            return;
        }

        voice.Volume = volume;
        _al.SetSourceProperty(voice.Source, SourceFloat.Gain, GainOf(voice.Channel, volume));
    }

    /// <inheritdoc />
    public float GetVolume(PlaybackHandle playback) => _voices.TryGet(playback.Voice, out Voice? voice) && voice is not null ? voice.Volume : 1f;

    /// <inheritdoc />
    public void SetPitch(PlaybackHandle playback, float pitch)
    {
        if (!_voices.TryGet(playback.Voice, out Voice? voice) || voice is null)
        {
            return;
        }

        voice.Pitch = pitch;
        _al.SetSourceProperty(voice.Source, SourceFloat.Pitch, MathF.Max(pitch, 0.1f));
    }

    /// <inheritdoc />
    public void SetPan(PlaybackHandle playback, float pan)
    {
        if (!_voices.TryGet(playback.Voice, out Voice? voice) || voice is null)
        {
            return;
        }

        voice.Pan = pan;
        _al.SetSourceProperty(voice.Source, SourceVector3.Position, Math.Clamp(pan, -1f, 1f), 0f, 0f);
    }

    /// <inheritdoc />
    public bool IsAlive(PlaybackHandle playback) =>
        playback.Voice.IsValid && _voices.TryGet(playback.Voice, out Voice? voice) && voice is not null;

    /// <inheritdoc />
    public void SetChannelVolume(AudioChannel channel, float volume)
    {
        _channelVolumes[(int)channel] = Math.Clamp(volume, 0f, 1f);
        RefreshChannel(channel);
    }

    /// <inheritdoc />
    public float GetChannelVolume(AudioChannel channel) => _channelVolumes[(int)channel];

    /// <summary>Returns the gain a voice of a channel is heard at, which is its own volume multiplied by the ones above it.</summary>
    private float GainOf(AudioChannel channel, float volume) =>
        Math.Clamp(volume, 0f, 1f) * _channelVolumes[(int)channel] * _channelVolumes[(int)AudioChannel.Master];

    /// <summary>Writes the gain of every voice of a channel, which is what changing a channel volume does.</summary>
    private void RefreshChannel(AudioChannel channel)
    {
        // A change of the master is heard by every voice, so every voice is written; a change of any other channel is
        // written only for the voices of that channel.
        bool master = channel == AudioChannel.Master;

        foreach (ResourceHandle slot in _voices.GetHandles())
        {
            if (_voices.TryGet(slot, out Voice? voice) && voice is not null && (master || voice.Channel == channel))
            {
                _al.SetSourceProperty(voice.Source, SourceFloat.Gain, GainOf(voice.Channel, voice.Volume));
            }
        }
    }

    /// <summary>Returns a voice for a playback of a given priority, or null when no voice could be taken.</summary>
    private Voice? TakeVoice(int priority)
    {
        if (_voices.Count < _voiceLimit)
        {
            uint source = _al.GenSource();
            var created = new Voice(source);
            ResourceHandle slot = _voices.Add(created);
            created.Resource = slot;
            return created;
        }

        // No voice is free: the playback of the lowest priority is stopped and its voice is handed to the one that is
        // starting, which is what a score that must be heard over an effect is for. A playback of a priority that no
        // running one is above finds no voice and is dropped, which is what a game that plays a hundred hits at once is
        // not warned about.
        ResourceHandle? stolen = null;
        int lowest = int.MaxValue;

        foreach (ResourceHandle slot in _voices.GetHandles())
        {
            if (_voices.TryGet(slot, out Voice? held) && held is not null && held.Priority < lowest && held.Priority < priority)
            {
                lowest = held.Priority;
                stolen = slot;
            }
        }

        if (stolen is ResourceHandle taken && _voices.TryGet(taken, out Voice? found) && found is not null)
        {
            _al.SourceStop(found.Source);
            return found;
        }

        return null;
    }

    /// <summary>Deletes the voices whose playback ended, so a game that plays often does not pile them up.</summary>
    private void ReleaseFinished()
    {
        foreach (ResourceHandle slot in _voices.GetHandles())
        {
            if (_voices.TryGet(slot, out Voice? voice) && voice is not null && !IsPlaying(voice))
            {
                Release(slot, voice);
            }
        }
    }

    /// <summary>Deletes the voices over the limit, oldest first, which is what lowering the limit asks for.</summary>
    private void ReleaseOverLimit()
    {
        if (_voices.Count <= _voiceLimit)
        {
            return;
        }

        foreach (ResourceHandle slot in _voices.GetHandles())
        {
            if (_voices.Count <= _voiceLimit)
            {
                break;
            }

            if (_voices.TryGet(slot, out Voice? voice) && voice is not null)
            {
                Release(slot, voice);
            }
        }
    }

    /// <summary>Determines whether a voice is still playing, whether it is paused or running.</summary>
    private bool IsPlaying(Voice voice)
    {
        _al.GetSourceProperty(voice.Source, GetSourceInteger.SourceState, out int state);

        return (SourceState)state is SourceState.Playing or SourceState.Paused;
    }

    /// <summary>Stops the playback of a voice and returns it to the pool, releasing the source it used.</summary>
    private void Release(ResourceHandle slot, Voice voice)
    {
        _al.SourceStop(voice.Source);
        _al.SetSourceProperty(voice.Source, SourceInteger.Buffer, 0u);
        _al.DeleteSource(voice.Source);
        _voices.Release(slot);
    }

    /// <summary>Stops the playbacks, deletes the sources and closes the device.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopAll();
        _alc.DestroyContext(_context);
        _alc.CloseDevice(_device);
        _al.Dispose();
        _alc.Dispose();
    }

    /// <summary>One voice of the pool: the OpenAL source it plays through, and the state a game wrote to it.</summary>
    private sealed class Voice(uint source)
    {
        public uint Source { get; } = source;
        public ResourceHandle Resource;
        public int SoundId;
        public AudioChannel Channel;
        public int Priority;
        public float Volume = 1f;
        public float Pitch = 1f;
        public float Pan;
    }
}
