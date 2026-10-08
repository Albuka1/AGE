using Age.Assets;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class SoundLoaderTests : IDisposable
{
    private readonly string _root;
    private readonly SoundLoader _loader;

    public SoundLoaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "age-soundformats-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        var assets = new NullAssetLoader();
        assets.Initialize(_root);
        _loader = new SoundLoader(assets);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData("sample.ogg")]
    [InlineData("sample.mp3")]
    public void SoundLoader_LoadACompressedSound_DecodesTheRecording(string name)
    {
        Copy(name);

        SoundData sound = _loader.Load(name);

        // Two seconds of the fixture, in the format both of them are stored in. The WAVE fixtures are covered by their
        // own test class, and a decoder that was handed the wrong format would fail here instead of throwing quietly.
        sound.SampleRate.Should().Be(44100);
        sound.Channels.Should().Be(2);
        sound.Duration.Should().BeCloseTo(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(250));
        sound.Samples.Count(sample => sample != 0).Should().BeGreaterThan(sound.Samples.Length / 2, "the decoded samples carry the recording");
    }

    [Fact]
    public void SoundLoader_LoadOgg_HandsTheFileToTheOggDecoder()
    {
        Write("music.ogg", [.. "OggS"u8, 0, 0, 0, 0]);

        Action act = () => _loader.Load("music.ogg");

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void SoundLoader_LoadMp3_HandsTheFileToTheMpegDecoder()
    {
        Write("tune.mp3", [0x49, 0x44, 0x33, 0x04, 0x00, 0x00, 0x00, 0x00]);

        Action act = () => _loader.Load("tune.mp3");

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void SoundLoader_LoadWave_HandsTheFileToTheWaveDecoder()
    {
        // A WAVE header without a data chunk, so the format is recognised and its own decoder reports the empty file.
        Write("blip.wav", [.. "RIFF"u8, 4, 0, 0, 0, .. "WAVE"u8]);

        Action act = () => _loader.Load("blip.wav");

        act.Should().Throw<InvalidDataException>().WithMessage("*no data chunk*");
    }

    [Fact]
    public void SoundLoader_LoadBytesOfNoKnownFormat_ReportsAnUnsupportedFormat()
    {
        Write("mystery.bin", "not a sound at all");

        Action act = () => _loader.Load("mystery.bin");

        act.Should().Throw<InvalidDataException>().WithMessage("*not a sound in a supported format*");
    }

    /// <summary>Puts a fixture next to the temporary root, because the asset loader works inside the game root.</summary>
    private void Copy(string name) =>
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "Resources", "Audio", "Samples", name),
            Path.Combine(_root, name),
            overwrite: true);

    private void Write(string relativePath, byte[] content) => File.WriteAllBytes(Path.Combine(_root, relativePath), content);

    private void Write(string relativePath, string content) => Write(relativePath, System.Text.Encoding.UTF8.GetBytes(content));
}
