using Age.Assets;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class WavSoundLoaderTests : IDisposable
{
    private readonly string _root;
    private readonly WavSoundLoader _loader;

    public WavSoundLoaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "age-sounds-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        var assets = new NullAssetLoader();
        assets.Initialize(_root);
        _loader = new WavSoundLoader(assets);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void SoundLoader_LoadSixteenBitMono_ReturnsTheSamplesAndTheirFormat()
    {
        Write("blip.wav", CreateWav(22050, channels: 1, bitsPerSample: 16, Samples16(0, 1000, -1000, 32767)));

        SoundData sound = _loader.Load("blip.wav");

        sound.SampleRate.Should().Be(22050);
        sound.Channels.Should().Be(1);
        sound.Samples.Should().Equal(0, 1000, -1000, 32767);
        sound.FrameCount.Should().Be(4);
        sound.Duration.Should().BeCloseTo(TimeSpan.FromSeconds(4d / 22050d), TimeSpan.FromMilliseconds(0.1));
    }

    [Fact]
    public void SoundLoader_LoadStereo_KeepsTheInterleavedOrder()
    {
        Write("stereo.wav", CreateWav(44100, channels: 2, bitsPerSample: 16, Samples16(10, 20, 30, 40)));

        SoundData sound = _loader.Load("stereo.wav");

        sound.Channels.Should().Be(2);
        sound.Samples.Should().Equal(10, 20, 30, 40);
        sound.FrameCount.Should().Be(2);
    }

    [Fact]
    public void SoundLoader_LoadEightBit_ConvertsTheUnsignedSamples()
    {
        // 8-bit WAVE samples are unsigned: 128 is the middle, 255 is the peak and 0 is the trough.
        Write("eight.wav", CreateWav(8000, channels: 1, bitsPerSample: 8, [128, 255, 0]));

        SoundData sound = _loader.Load("eight.wav");

        sound.Samples.Should().Equal(0, 32512, -32768);
    }

    [Fact]
    public void SoundLoader_LoadTwentyFourBit_KeepsTheHighBits()
    {
        Write("deep.wav", CreateWav(48000, channels: 1, bitsPerSample: 24, Samples24(0x7FFFFF, 0x800000, 0x400000)));

        SoundData sound = _loader.Load("deep.wav");

        sound.Samples.Should().Equal(32767, -32768, 16384);
    }

    [Fact]
    public void SoundLoader_LoadFloat_ConvertsToSixteenBit()
    {
        Write("float.wav", CreateWav(44100, channels: 1, bitsPerSample: 32, SamplesFloat(0f, 0.5f, -0.5f, 1f), format: 3));

        SoundData sound = _loader.Load("float.wav");

        sound.Samples.Should().Equal(0, 16383, -16383, 32767);
    }

    [Fact]
    public void SoundLoader_LoadFileWithoutData_ThrowsInvalidDataException()
    {
        byte[] bytes = CreateWav(8000, channels: 1, bitsPerSample: 16, []);

        Write("empty.wav", bytes[..36]);

        Action act = () => _loader.Load("empty.wav");

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void SoundLoader_LoadACompressedFormat_ThrowsInvalidDataException()
    {
        // Format 2 is ADPCM, which this loader does not decode.
        Write("adpcm.wav", CreateWav(8000, channels: 1, bitsPerSample: 4, [1, 2, 3], format: 2));

        Action act = () => _loader.Load("adpcm.wav");

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void SoundLoader_LoadNotAWave_ThrowsInvalidDataException()
    {
        Write("broken.wav", "this is not a sound");

        Action act = () => _loader.Load("broken.wav");

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void SoundLoader_LoadMissingFile_ThrowsFileNotFoundException()
    {
        Action act = () => _loader.Load("missing.wav");

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void SoundData_SampleBufferWithoutWholeFrames_ThrowsArgumentException()
    {
        Action act = () => new SoundData(44100, channels: 2, [1, 2, 3]);

        act.Should().Throw<ArgumentException>();
    }

    private static byte[] CreateWav(int sampleRate, int channels, int bitsPerSample, byte[] samples, int format = 1)
    {
        int frameSize = bitsPerSample / 8 * channels;
        byte[] bytes = new byte[44 + samples.Length];

        WriteTag(bytes, 0, "RIFF");
        WriteInt32(bytes, 4, bytes.Length - 8);
        WriteTag(bytes, 8, "WAVE");
        WriteTag(bytes, 12, "fmt ");
        WriteInt32(bytes, 16, 16);
        WriteInt16(bytes, 20, format);
        WriteInt16(bytes, 22, channels);
        WriteInt32(bytes, 24, sampleRate);
        WriteInt32(bytes, 28, sampleRate * frameSize);
        WriteInt16(bytes, 32, frameSize);
        WriteInt16(bytes, 34, bitsPerSample);
        WriteTag(bytes, 36, "data");
        WriteInt32(bytes, 40, samples.Length);
        samples.CopyTo(bytes, 44);
        return bytes;
    }

    private static byte[] Samples16(params short[] values)
    {
        byte[] bytes = new byte[values.Length * 2];

        for (int index = 0; index < values.Length; index++)
        {
            WriteInt16(bytes, index * 2, values[index]);
        }

        return bytes;
    }

    private static byte[] Samples24(params int[] values)
    {
        byte[] bytes = new byte[values.Length * 3];

        for (int index = 0; index < values.Length; index++)
        {
            int value = values[index];
            bytes[index * 3] = (byte)value;
            bytes[(index * 3) + 1] = (byte)(value >> 8);
            bytes[(index * 3) + 2] = (byte)(value >> 16);
        }

        return bytes;
    }

    private static byte[] SamplesFloat(params float[] values)
    {
        byte[] bytes = new byte[values.Length * 4];

        for (int index = 0; index < values.Length; index++)
        {
            BitConverter.GetBytes(values[index]).CopyTo(bytes, index * 4);
        }

        return bytes;
    }

    private static void WriteTag(byte[] bytes, int offset, string tag)
    {
        for (int index = 0; index < tag.Length; index++)
        {
            bytes[offset + index] = (byte)tag[index];
        }
    }

    private static void WriteInt16(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteInt32(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
    }

    private void Write(string relativePath, byte[] content) => File.WriteAllBytes(Path.Combine(_root, relativePath), content);

    private void Write(string relativePath, string content) => Write(relativePath, System.Text.Encoding.UTF8.GetBytes(content));
}
