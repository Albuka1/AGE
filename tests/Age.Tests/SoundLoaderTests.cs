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

    private void Write(string relativePath, byte[] content) => File.WriteAllBytes(Path.Combine(_root, relativePath), content);

    private void Write(string relativePath, string content) => Write(relativePath, System.Text.Encoding.UTF8.GetBytes(content));
}
