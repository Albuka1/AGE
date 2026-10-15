using Age.Audio;
using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class AudioFaderTests
{
    // A frame of the fader is the time that one frame of the display took: a fade of a channel or of a playback is
    // advanced by what the frame reports, and what a test asks about is what the fade left in the device.
    private static GameTime Frame(double delta, double total = 0d) => new(delta, total);

    [Fact]
    public void AudioFader_Fade_Channel_ReachesTheTargetOverTheDuration()
    {
        var audio = new FakeAudioService();
        var fader = new AudioFader(audio);

        fader.Fade(AudioChannel.Music, 0.5f, 1f);

        // Halfway: half of the change is done, which is what an unrounded fade of one second looks like.
        fader.UpdateFrame(new World(), Frame(0.5d));
        audio.GetChannelVolume(AudioChannel.Music).Should().BeApproximately(0.75f, 0.001f);

        // At the end: the target is written whole, which is what a caller reads when it asks for the volume of the channel.
        fader.UpdateFrame(new World(), Frame(0.5d, 1d));
        audio.GetChannelVolume(AudioChannel.Music).Should().Be(0.5f);
    }

    [Fact]
    public void AudioFader_Fade_ChannelWithZeroDuration_SetsItAtOnce()
    {
        var audio = new FakeAudioService();
        var fader = new AudioFader(audio);

        fader.Fade(AudioChannel.Music, 0.25f, 0f);

        audio.GetChannelVolume(AudioChannel.Music).Should().Be(0.25f);
        audio.ChannelWrites.Should().HaveCount(1);
    }

    [Fact]
    public void AudioFader_Fade_TheSecondOneOfAChannel_ReplacesTheFirst()
    {
        var audio = new FakeAudioService();
        var fader = new AudioFader(audio);

        fader.Fade(AudioChannel.Music, 0.5f, 1f);
        fader.Fade(AudioChannel.Music, 0f, 1f);

        fader.UpdateFrame(new World(), Frame(1d));

        // The first fade would have reached 0.5, and the second one reaches 0: the two are one fade of the channel, and the
        // last one is the one that was asked for.
        audio.GetChannelVolume(AudioChannel.Music).Should().Be(0f);
    }

    [Fact]
    public void AudioFader_Fade_Playback_ReachesTheTargetOverTheDuration()
    {
        var audio = new FakeAudioService();
        var fader = new AudioFader(audio);

        PlaybackHandle playback = audio.Play(1, new AudioPlaybackSettings { Volume = 1f, Channel = AudioChannel.Effects });
        fader.Fade(playback, 0f, 1f);

        fader.UpdateFrame(new World(), Frame(0.5d));
        audio.GetVolume(playback).Should().BeApproximately(0.5f, 0.001f);

        fader.UpdateFrame(new World(), Frame(0.5d, 1d));
        audio.GetVolume(playback).Should().Be(0f);

        // A fade alone does not stop the voice: the sound that reached silence is still heard from, which is what a game that
        // turns the score down and back up asks for.
        audio.IsAlive(playback).Should().BeTrue();
        audio.Stopped.Should().BeEmpty();
    }

    [Fact]
    public void AudioFader_FadeAndStop_StopsThePlaybackAtTheEnd()
    {
        var audio = new FakeAudioService();
        var fader = new AudioFader(audio);

        PlaybackHandle playback = audio.Play(1, new AudioPlaybackSettings { Volume = 1f, Channel = AudioChannel.Effects });
        fader.FadeAndStop(playback, 1f);

        fader.UpdateFrame(new World(), Frame(0.5d));
        audio.IsAlive(playback).Should().BeTrue();

        fader.UpdateFrame(new World(), Frame(0.5d, 1d));
        audio.IsAlive(playback).Should().BeFalse();
        audio.Stopped.Should().Contain(1);
    }

    [Fact]
    public void AudioFader_FadeAndStop_WithZeroDuration_StopsAtOnce()
    {
        var audio = new FakeAudioService();
        var fader = new AudioFader(audio);

        PlaybackHandle playback = audio.Play(1, AudioPlaybackSettings.Default(AudioChannel.Effects));
        fader.FadeAndStop(playback, 0f);

        audio.IsAlive(playback).Should().BeFalse();
        audio.Stopped.Should().Contain(1);
    }

    [Fact]
    public void AudioFader_Fade_APlaybackThatIsGone_DoesNothing()
    {
        var audio = new FakeAudioService();
        var fader = new AudioFader(audio);

        PlaybackHandle playback = audio.Play(1, AudioPlaybackSettings.Default(AudioChannel.Effects));
        audio.Stop(playback);

        fader.Fade(playback, 0f, 1f);
        fader.UpdateFrame(new World(), Frame(1d));

        // A handle that the device does not hold any more is a handle of a sound that is not heard: the fader leaves it
        // alone rather than writing a volume to a voice that was returned to the pool.
        audio.VolumeWrites.Should().BeEmpty();
    }

    [Fact]
    public void AudioFader_Fade_APlaybackThatEndsMidFade_ForgetsTheSchedule()
    {
        var audio = new FakeAudioService();
        var fader = new AudioFader(audio);

        PlaybackHandle playback = audio.Play(1, AudioPlaybackSettings.Default(AudioChannel.Effects));
        fader.Fade(playback, 0f, 1f);
        fader.UpdateFrame(new World(), Frame(0.5d));

        // The sound ends on its own while the fade is still running, which is what a sound that does not loop does: the
        // frame after that finds no voice to write and forgets the schedule rather than holding it forever.
        audio.Stop(playback);
        fader.UpdateFrame(new World(), Frame(0.5d, 1d));

        int writes = audio.VolumeWrites.Count;

        fader.UpdateFrame(new World(), Frame(0.5d, 1.5d));
        audio.VolumeWrites.Should().HaveCount(writes);
    }

    [Fact]
    public void AudioFader_Fade_TheSecondOneOfAPlayback_ReplacesTheFirst()
    {
        var audio = new FakeAudioService();
        var fader = new AudioFader(audio);

        PlaybackHandle playback = audio.Play(1, new AudioPlaybackSettings { Volume = 1f, Channel = AudioChannel.Effects });

        fader.Fade(playback, 0.5f, 1f);
        fader.FadeAndStop(playback, 1f);

        fader.UpdateFrame(new World(), Frame(1d));

        // The second fade is the one that was asked for last: the playback is stopped rather than left at 0.5.
        audio.IsAlive(playback).Should().BeFalse();
        audio.Stopped.Should().Contain(1);
    }

    [Fact]
    public void AudioFader_Cancel_Channel_StopsTheFadeAtTheVolumeItReached()
    {
        var audio = new FakeAudioService();
        var fader = new AudioFader(audio);

        fader.Fade(AudioChannel.Music, 0f, 1f);
        fader.UpdateFrame(new World(), Frame(0.5d));

        int cancelled = fader.Cancel(AudioChannel.Music);
        cancelled.Should().Be(1);

        float held = audio.GetChannelVolume(AudioChannel.Music);

        fader.UpdateFrame(new World(), Frame(0.5d, 1d));

        // A cancelled fade leaves the channel where it stood, which is what a game that interrupts itself with a new value
        // wants to be able to read: the volume it started the new fade from is the one the old one reached.
        audio.GetChannelVolume(AudioChannel.Music).Should().Be(held);
    }

    [Fact]
    public void AudioFader_Cancel_ChannelThatIsNotFading_DoesNothing()
    {
        var audio = new FakeAudioService();
        var fader = new AudioFader(audio);

        fader.Cancel(AudioChannel.Music).Should().Be(0);
    }

    [Fact]
    public void AudioFader_Cancel_Playback_StopsTheFadeAndLeavesTheVoiceAlive()
    {
        var audio = new FakeAudioService();
        var fader = new AudioFader(audio);

        PlaybackHandle playback = audio.Play(1, new AudioPlaybackSettings { Volume = 1f, Channel = AudioChannel.Effects });
        fader.FadeAndStop(playback, 1f);
        fader.UpdateFrame(new World(), Frame(0.5d));

        int cancelled = fader.Cancel(playback);
        cancelled.Should().Be(1);

        fader.UpdateFrame(new World(), Frame(0.5d, 1d));

        // A cancelled fade is cancelled whole, including the stop: the voice that the fade would have returned to the pool is
        // still there, which is what a game that changes its mind in the middle of a fade-out hears.
        audio.IsAlive(playback).Should().BeTrue();
        audio.Stopped.Should().BeEmpty();
    }

    [Fact]
    public void AudioFader_Fade_TheSameChannelFromSeveralFrames_KeepsOneSchedule()
    {
        var audio = new FakeAudioService();
        var fader = new AudioFader(audio);

        for (int frame = 0; frame < 30; frame++)
        {
            fader.Fade(AudioChannel.Music, 0.25f, 1f);
            fader.UpdateFrame(new World(), Frame(1d / 60d));
        }

        // Thirty frames of a game that asks for the same fade every frame is one schedule, not thirty: the volume of the
        // channel is a value of the fade that was last asked for, not a sum of every request.
        audio.ChannelWrites.Should().HaveCount(30);
        audio.GetChannelVolume(AudioChannel.Music).Should().BeApproximately(1f, 0.001f);
    }
}
