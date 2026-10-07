using Age.Assets;
using Silk.NET.OpenAL;

namespace Age.Audio;

/// <summary>
/// An <see cref="IAudioService"/> backed by OpenAL through Silk.NET. It opens the default device when it is created
/// and holds every sound in an OpenAL buffer.
/// </summary>
/// <remarks>
/// This service is not registered by <c>AddAgeAudio</c>, because a machine without audio would fail to start: register
/// it with <c>AddAgeOpenALAudio</c> when the game plays sound and leave the null device in place for headless runs.
/// </remarks>
public sealed unsafe class OpenALAudioService : IAudioService, IDisposable
{
    private readonly ALContext _alc;
    private readonly AL _al;
    private readonly Device* _device;
    private readonly Context* _context;
    private readonly List<uint> _sources = [];

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
    }

    /// <inheritdoc />
    public int CreateSound(SoundData sound)
    {
        ArgumentNullException.ThrowIfNull(sound);

        uint buffer = _al.GenBuffer();
        _al.BufferData(buffer, sound.Channels == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16, sound.Samples, sound.SampleRate);

        AudioError error = _al.GetError();
        if (error != AudioError.NoError)
        {
            _al.DeleteBuffer(buffer);
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

        _al.DeleteBuffer((uint)soundId);
    }

    /// <inheritdoc />
    public void PlaySound(int soundId, float volume = 1f, bool loop = false)
    {
        if (soundId == 0)
        {
            return;
        }

        DeleteFinishedSources();

        uint source = _al.GenSource();
        _al.SetSourceProperty(source, SourceInteger.Buffer, (uint)soundId);
        _al.SetSourceProperty(source, SourceFloat.Gain, Math.Clamp(volume, 0f, 1f));
        _al.SetSourceProperty(source, SourceBoolean.Looping, loop);
        _al.SourcePlay(source);
        _sources.Add(source);
    }

    /// <inheritdoc />
    public void StopAll()
    {
        foreach (uint source in _sources)
        {
            _al.SourceStop(source);
            _al.DeleteSource(source);
        }

        _sources.Clear();
    }

    /// <inheritdoc />
    public void SetMasterVolume(float volume) => _al.SetListenerProperty(ListenerFloat.Gain, Math.Clamp(volume, 0f, 1f));

    /// <summary>Deletes the sources whose playback ended, so a game that plays often does not pile them up.</summary>
    private void DeleteFinishedSources()
    {
        for (int index = _sources.Count - 1; index >= 0; index--)
        {
            uint source = _sources[index];
            _al.GetSourceProperty(source, GetSourceInteger.SourceState, out int state);

            if ((SourceState)state == SourceState.Playing)
            {
                continue;
            }

            _al.DeleteSource(source);
            _sources.RemoveAt(index);
        }
    }

    /// <summary>Stops the playbacks, deletes the sources and closes the device.</summary>
    public void Dispose()
    {
        StopAll();
        _alc.DestroyContext(_context);
        _alc.CloseDevice(_device);
        _al.Dispose();
        _alc.Dispose();
    }
}
