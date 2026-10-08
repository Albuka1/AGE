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
    public void SoundService_Play_SendsTheSoundToTheDevice()
    {
        var audio = new FakeAudioService();
        var sounds = new SoundService(new FakeSoundLoader(), audio);
        SoundHandle handle = sounds.Load("sfx/click.wav");

        sounds.Play(handle, volume: 0.5f, loop: true);

        audio.Played.Should().HaveCount(1);
        audio.Played[0].Id.Should().Be(handle.Id);
        audio.Played[0].Volume.Should().Be(0.5f);
        audio.Played[0].Loop.Should().BeTrue();
    }

    [Fact]
    public void SoundService_Play_AHandleTheServiceDoesNotOwn_PlaysNothing()
    {
        var audio = new FakeAudioService();
        var sounds = new SoundService(new FakeSoundLoader(), audio);

        sounds.Play(new SoundHandle(7));

        audio.Played.Should().BeEmpty();
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
        Action play = () => sounds.Play(handle);
        play.Should().NotThrow();
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
        // The null device reports no sound, which is the identifier that playing ignores.
        var sounds = new SoundService(new FakeSoundLoader(), new NullAudioService());

        SoundHandle handle = sounds.Load("sfx/click.wav");

        handle.Id.Should().Be(0);
        sounds.IsAlive(handle).Should().BeTrue();
        Action play = () => sounds.Play(handle);
        play.Should().NotThrow();
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

    private sealed class FakeAudioService : IAudioService
    {
        public List<int> Created { get; } = [];
        public List<int> Deleted { get; } = [];
        public List<(int Id, float Volume, bool Loop)> Played { get; } = [];
        public SoundData? LastSound { get; private set; }
        public Func<int, bool>? FailDelete { get; set; }

        public int CreateSound(SoundData sound)
        {
            LastSound = sound;

            int id = Created.Count + 1;
            Created.Add(id);
            return id;
        }

        public void DeleteSound(int soundId)
        {
            Deleted.Add(soundId);

            if (FailDelete?.Invoke(soundId) == true)
            {
                throw new InvalidOperationException($"The device refused to delete sound {soundId}.");
            }
        }

        public void PlaySound(int soundId, float volume = 1f, bool loop = false) => Played.Add((soundId, volume, loop));

        public void StopAll()
        {
        }

        public void SetMasterVolume(float volume)
        {
        }
    }
}
