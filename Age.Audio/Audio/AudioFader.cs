using Age.Core;

namespace Age.Audio;

/// <summary>
/// The default <see cref="IAudioFader"/>: a frame system that advances what is scheduled by the time of the frame.
/// </summary>
/// <remarks>
/// The fader is a frame system, so a game adds it to its pipeline with <c>AddFrame</c> and the clock it advances on is
/// the one of the display: a fade of the score outlasts a pause of the simulation, which is what a game wants when a
/// menu holds the game while the music keeps playing.
/// </remarks>
public sealed class AudioFader : IAudioFader, IFrameSystem
{
    private readonly IAudioService _audio;
    private readonly Dictionary<AudioChannel, Scheduled> _channels = new();
    private readonly Dictionary<ResourceHandle, Scheduled> _playbacks = new();
    private readonly List<AudioChannel> _finishedChannels = new();
    private readonly List<ResourceHandle> _finishedPlaybacks = new();

    /// <summary>Initializes the fader with the device whose volumes it writes.</summary>
    /// <param name="audio">The device that mixes the sounds.</param>
    /// <exception cref="ArgumentNullException">The device is null.</exception>
    public AudioFader(IAudioService audio)
    {
        ArgumentNullException.ThrowIfNull(audio);
        _audio = audio;
    }

    /// <inheritdoc />
    public void Fade(AudioChannel channel, float to, float duration, TweenEasing easing = TweenEasing.Linear)
    {
        if (duration <= 0f)
        {
            _channels.Remove(channel);
            _audio.SetChannelVolume(channel, to);
            return;
        }

        _channels[channel] = new Scheduled(_audio.GetChannelVolume(channel), to, duration, easing, stopWhenDone: false);
    }

    /// <inheritdoc />
    public void Fade(PlaybackHandle playback, float to, float duration, TweenEasing easing = TweenEasing.Linear)
    {
        if (!_audio.IsAlive(playback))
        {
            return;
        }

        if (duration <= 0f)
        {
            _playbacks.Remove(playback.Voice);
            _audio.SetVolume(playback, to);
            return;
        }

        _playbacks[playback.Voice] = new Scheduled(_audio.GetVolume(playback), to, duration, easing, stopWhenDone: false);
    }

    /// <inheritdoc />
    public void FadeAndStop(PlaybackHandle playback, float duration, TweenEasing easing = TweenEasing.Linear)
    {
        if (!_audio.IsAlive(playback))
        {
            return;
        }

        if (duration <= 0f)
        {
            _playbacks.Remove(playback.Voice);
            _audio.Stop(playback);
            return;
        }

        _playbacks[playback.Voice] = new Scheduled(_audio.GetVolume(playback), 0f, duration, easing, stopWhenDone: true);
    }

    /// <inheritdoc />
    public int Cancel(AudioChannel channel) => _channels.Remove(channel) ? 1 : 0;

    /// <inheritdoc />
    public int Cancel(PlaybackHandle playback) => _playbacks.Remove(playback.Voice) ? 1 : 0;

    /// <inheritdoc />
    public void UpdateFrame(World world, in GameTime frame)
    {
        ArgumentNullException.ThrowIfNull(world);
        Advance((float)frame.Delta);
    }

    /// <summary>Advances every fade that is scheduled and forgets the ones that reached their volume.</summary>
    private void Advance(float delta)
    {
        // A channel fade never stops anything: it only writes a volume, and the channel itself is a value of the
        // device rather than a voice, so it stays where the fade left it.
        _finishedChannels.Clear();

        foreach ((AudioChannel channel, Scheduled scheduled) in _channels)
        {
            if (scheduled.Advance(delta, out float volume))
            {
                _audio.SetChannelVolume(channel, volume);
            }
            else
            {
                _audio.SetChannelVolume(channel, scheduled.To);
                _finishedChannels.Add(channel);
            }
        }

        foreach (AudioChannel channel in _finishedChannels)
        {
            _channels.Remove(channel);
        }

        // A playback fade writes a volume, and one that was asked to stop stops the playback on the frame it reaches
        // its end: the voice is returned to the device there, so the sound is not heard after the fade, and a game
        // that plays many sounds does not hold voices for the ones it is done with.
        _finishedPlaybacks.Clear();

        foreach ((ResourceHandle voice, Scheduled scheduled) in _playbacks)
        {
            var playback = new PlaybackHandle(voice);

            if (!_audio.IsAlive(playback))
            {
                // The sound ended on its own while the fade ran, which a fade of a sound that does not loop does: the
                // schedule is forgotten rather than written, because there is no voice to write a volume to.
                _finishedPlaybacks.Add(voice);
                continue;
            }

            if (scheduled.Advance(delta, out float volume))
            {
                _audio.SetVolume(playback, volume);
                continue;
            }

            if (scheduled.StopWhenDone)
            {
                _audio.Stop(playback);
            }
            else
            {
                _audio.SetVolume(playback, scheduled.To);
            }

            _finishedPlaybacks.Add(voice);
        }

        foreach (ResourceHandle voice in _finishedPlaybacks)
        {
            _playbacks.Remove(voice);
        }
    }

    /// <summary>One fade that is running: the volume it started at, the one it is going to, and how long it takes.</summary>
    private sealed class Scheduled
    {
        private readonly float _from;
        private readonly float _to;
        private readonly float _duration;
        private readonly TweenEasing _easing;
        private float _elapsed;

        public Scheduled(float from, float to, float duration, TweenEasing easing, bool stopWhenDone)
        {
            _from = from;
            _to = to;
            _duration = duration;
            _easing = easing;
            StopWhenDone = stopWhenDone;
        }

        /// <summary>Gets the volume the fade reaches at its end.</summary>
        public float To => _to;

        /// <summary>Gets a value indicating whether the playback that the fade belongs to stops when the fade reaches its end.</summary>
        public bool StopWhenDone { get; }

        /// <summary>Advances the fade by the time of a frame and reports whether it is still running.</summary>
        /// <param name="delta">The time to advance by, in seconds.</param>
        /// <param name="volume">Receives the volume the fade holds now.</param>
        /// <returns><see langword="false"/> when the fade reached its end.</returns>
        public bool Advance(float delta, out float volume)
        {
            _elapsed += delta;

            if (_elapsed >= _duration)
            {
                volume = _to;
                return false;
            }

            float progress = _duration > 0f ? _elapsed / _duration : 1f;
            volume = _from + ((_to - _from) * Ease(progress));
            return true;
        }

        private float Ease(float progress) => _easing switch
        {
            TweenEasing.EaseIn => progress * progress,
            TweenEasing.EaseOut => 1f - ((1f - progress) * (1f - progress)),
            TweenEasing.EaseInOut => progress < 0.5f ? 2f * progress * progress : 1f - (2f * (1f - progress) * (1f - progress)),
            _ => progress,
        };
    }
}