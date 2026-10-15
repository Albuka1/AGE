using Age.Assets;
using Age.Audio;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class SoundServiceTests
{
    [Fact]
    public void SoundService_LoadTwice_UploadsOnceAndReturnsTheSameHandle()
    {
        var loader = new FakeSoundLoader();
        var audio = new FakeAudioService();
        var sounds = new SoundService(loader, audio);

        SoundHandle first = sounds.Load("sfx/click.wav");
        SoundHandle second = sounds.Load("sfx/click.wav");

        loader.Calls.Should().Be(1);
        audio.Created.Should().HaveCount(1);
        second.Should().Be(first);
        sounds.Count.Should().Be(1);
    }

    [Fact]
    public void SoundService_Load_UploadsTheDecodedSound()
    {
        var audio = new FakeAudioService();
        var sounds = new SoundService(new FakeSoundLoader(), audio);

        sounds.Load("sfx/click.wav");

        audio.LastSound!.SampleRate.Should().Be(8000);
        audio.LastSound.Channels.Should().Be(1);
        audio.LastSound.Samples.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void SoundService_Play_SendsTheSettingsToTheDevice()
    {
        var audio = new FakeAudioService();
        var sounds = new SoundService(new FakeSoundLoader(), audio);
        SoundHandle handle = sounds.Load("sfx/click.wav");

        var settings = new AudioPlaybackSettings
        {
            Volume = 0.5f,
            Pitch = 1.25f,
            Pan = -0.5f,
            Loop = true,
            Channel = AudioChannel.Music,
            Priority = 7,
        };

        PlaybackHandle playback = sounds.Play(handle, settings);

        playback.IsValid.Should().BeTrue();
        audio.Played.Should().HaveCount(1);
        audio.Played[0].SoundId.Should().Be(handle.Id);
        audio.Played[0].Settings.Volume.Should().Be(0.5f);
        audio.Played[0].Settings.Pitch.Should().Be(1.25f);
        audio.Played[0].Settings.Pan.Should().Be(-0.5f);
        audio.Played[0].Settings.Loop.Should().BeTrue();
        audio.Played[0].Settings.Channel.Should().Be(AudioChannel.Music);
        audio.Played[0].Settings.Priority.Should().Be(7);
    }

    [Fact]
    public void SoundService_Play_AnEmptyHandle_PlaysNothing()
    {
        var audio = new FakeAudioService();
        var sounds = new SoundService(new FakeSoundLoader(), audio);

        PlaybackHandle playback = sounds.Play(default, AudioPlaybackSettings.Default(AudioChannel.Effects));

        playback.IsValid.Should().BeFalse();
        audio.Played.Should().BeEmpty();
    }

    [Fact]
    public void SoundService_Play_AHandleTheServiceDoesNotOwn_PlaysNothing()
    {
        var audio = new FakeAudioService();
        var sounds = new SoundService(new FakeSoundLoader(), audio);

        // The number of a sound the device knows and no slot of this service: it plays nothing rather than reaching the
        // device with a voice of its own.
        var external = new SoundHandle(7);

        PlaybackHandle playback = sounds.Play(external, AudioPlaybackSettings.Default(AudioChannel.Effects));

        playback.IsValid.Should().BeFalse();
        audio.Played.Should().BeEmpty();
    }

    [Fact]
    public void SoundService_APlaybackThatTookTheVoiceOfAnother_DoesNotResolveTheHandleOfIt()
    {
        // The pool holds one voice, so the second play takes the voice of the first, which is what the device does when
        // every voice is busy and a playback of a higher priority is starting. The handle of the playback that was
        // displaced does not resolve any more: a game that kept it writes nothing of the sound that took its voice, and
        // a fade that was scheduled for it does not stop it.
        var audio = new FakeAudioService { VoiceLimit = 1 };
        var sounds = new SoundService(new FakeSoundLoader(), audio);

        SoundHandle quiet = sounds.Load("sfx/quiet.wav");
        SoundHandle loud = sounds.Load("sfx/loud.wav");

        PlaybackHandle first = sounds.Play(quiet, new AudioPlaybackSettings { Volume = 1f, Channel = AudioChannel.Effects, Priority = 0 });
        PlaybackHandle second = sounds.Play(loud, new AudioPlaybackSettings { Volume = 1f, Channel = AudioChannel.Music, Priority = 10 });

        audio.IsAlive(first).Should().BeFalse("the voice of the quiet playback was taken, so its handle names no voice of the device");
        audio.IsAlive(second).Should().BeTrue("the playback that took the voice is the one the device still holds");
        audio.Stopped.Should().Contain(quiet.Id, "the displaced playback was stopped rather than left to be heard over the one that took its voice");

        // What a game that kept the old handle does is nothing, rather than reaching the sound that took its voice.
        audio.SetVolume(first, 0f);
        audio.GetVolume(second).Should().Be(1f, "the volume of the playback that took the voice is not the one the old handle was told");

        audio.Stop(first);
        audio.IsAlive(second).Should().BeTrue("the old handle does not stop the playback that took its voice");
    }

    [Fact]
    public void SoundService_APlaybackOfNoLowerPriorityThanEveryRunningOne_IsDropped()
    {
        // The pool holds one voice and the running playback is of a higher priority than the one that is starting, so no
        // voice is taken and the request is dropped rather than a sound of the game being stopped to make room for it.
        var audio = new FakeAudioService { VoiceLimit = 1 };
        var sounds = new SoundService(new FakeSoundLoader(), audio);

        SoundHandle loud = sounds.Load("sfx/loud.wav");
        SoundHandle quiet = sounds.Load("sfx/quiet.wav");

        PlaybackHandle kept = sounds.Play(loud, new AudioPlaybackSettings { Volume = 1f, Channel = AudioChannel.Music, Priority = 10 });
        PlaybackHandle dropped = sounds.Play(quiet, new AudioPlaybackSettings { Volume = 1f, Channel = AudioChannel.Effects, Priority = 0 });

        dropped.IsValid.Should().BeFalse("no running playback is of a lower priority, so there is no voice to take");
        audio.IsAlive(kept).Should().BeTrue("the playback that was heard is not stopped for the one that was dropped");
        audio.Stopped.Should().BeEmpty();
    }

    [Fact]
    public void SoundService_Unload_DeletesTheSoundAndInvalidatesTheHandle()
    {
        var audio = new FakeAudioService();
        var sounds = new SoundService(new FakeSoundLoader(), audio);
        SoundHandle handle = sounds.Load("sfx/click.wav");

        bool unloaded = sounds.Unload(handle);

        unloaded.Should().BeTrue();
        audio.Deleted.Should().Equal(audio.Created);
        sounds.IsAlive(handle).Should().BeFalse();
        sounds.Count.Should().Be(0);
        sounds.Unload(handle).Should().BeFalse();

        PlaybackHandle playback = sounds.Play(handle, AudioPlaybackSettings.Default(AudioChannel.Effects));
        playback.IsValid.Should().BeFalse();
    }

    [Fact]
    public void SoundService_LoadAfterUnload_UploadsAgain()
    {
        var loader = new FakeSoundLoader();
        var audio = new FakeAudioService();
        var sounds = new SoundService(loader, audio);
        SoundHandle first = sounds.Load("sfx/click.wav");
        sounds.Unload(first);

        SoundHandle second = sounds.Load("sfx/click.wav");

        loader.Calls.Should().Be(2);
        audio.Created.Should().HaveCount(2);
        second.Should().NotBe(first);
        sounds.IsAlive(first).Should().BeFalse();
        sounds.IsAlive(second).Should().BeTrue();
    }

    [Fact]
    public void SoundService_IsAlive_IsFalseForAHandleTheDeviceMade()
    {
        var sounds = new SoundService(new FakeSoundLoader(), new FakeAudioService());

        sounds.IsAlive(new SoundHandle(7)).Should().BeFalse();
    }

    [Fact]
    public void SoundService_UnloadAll_WhenADeleteFails_KeepsThatSoundAndDeletesTheRest()
    {
        var audio = new FakeAudioService { FailDelete = id => id == 1 };
        var sounds = new SoundService(new FakeSoundLoader(), audio);
        SoundHandle first = sounds.Load("sfx/a.wav");
        SoundHandle second = sounds.Load("sfx/b.wav");

        Action unloadAll = sounds.UnloadAll;

        unloadAll.Should().Throw<InvalidOperationException>();
        audio.Deleted.Should().Equal(1, 2);
        sounds.Count.Should().Be(1);
        sounds.IsAlive(first).Should().BeTrue();
        sounds.IsAlive(second).Should().BeFalse();
    }

    [Fact]
    public void SoundService_UnloadAll_AfterAFailedDelete_RetriesTheSound()
    {
        var audio = new FakeAudioService { FailDelete = id => id == 1 };
        var sounds = new SoundService(new FakeSoundLoader(), audio);
        SoundHandle handle = sounds.Load("sfx/a.wav");
        Action firstAttempt = sounds.UnloadAll;
        firstAttempt.Should().Throw<InvalidOperationException>();

        audio.FailDelete = null;
        sounds.UnloadAll();

        audio.Deleted.Should().Equal(1, 1);
        sounds.Count.Should().Be(0);
        sounds.IsAlive(handle).Should().BeFalse();
    }

    [Fact]
    public void SoundService_Dispose_DeletesEverySound()
    {
        var audio = new FakeAudioService();
        var sounds = new SoundService(new FakeSoundLoader(), audio);
        sounds.Load("sfx/a.wav");

        sounds.Dispose();

        audio.Deleted.Should().HaveCount(1);
        sounds.Count.Should().Be(0);
    }

    [Fact]
    public void SoundService_WithTheNullDevice_LoadsAndPlaysWithoutADevice()
    {
        // The null device reports no sound, which is the handle that every play of it ignores.
        var sounds = new SoundService(new FakeSoundLoader(), new NullAudioService());

        SoundHandle handle = sounds.Load("sfx/click.wav");

        handle.Id.Should().Be(0);
        sounds.IsAlive(handle).Should().BeTrue();

        // A device that discards every playback answers a handle that names none of its voices, which is what a fade of
        // a sound that is not heard reads as "nothing to do".
        PlaybackHandle playback = sounds.Play(handle, AudioPlaybackSettings.Default(AudioChannel.Effects));
        playback.IsValid.Should().BeFalse();

        sounds.Unload(handle).Should().BeTrue();
    }

    private sealed class FakeSoundLoader : ISoundLoader
    {
        public int Calls { get; private set; }

        public SoundData Load(string relativePath)
        {
            Calls++;
            return new SoundData(8000, 1, [1, 2, 3, 4]);
        }
    }
}
