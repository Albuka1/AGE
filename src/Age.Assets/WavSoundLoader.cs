using System.Buffers.Binary;
using System.Text;

namespace Age.Assets;

/// <summary>
/// The default <see cref="ISoundLoader"/>. It reads RIFF WAVE files and converts their samples to signed 16-bit values.
/// The file itself is read through <see cref="IAssetLoader"/>, so every path stays inside the game root. Register
/// another <see cref="ISoundLoader"/> when a different format is needed.
/// </summary>
public sealed class WavSoundLoader : ISoundLoader
{
    private const int RiffHeaderSize = 12;
    private const int ChunkHeaderSize = 8;
    private const int FormatBodySize = 16;

    /// <summary>PCM samples.</summary>
    private const int PcmFormat = 1;

    /// <summary>IEEE float samples.</summary>
    private const int FloatFormat = 3;

    /// <summary>The tag of a WAVE_FORMAT_EXTENSIBLE header, whose real format sits in its sub format field.</summary>
    private const int ExtensibleFormat = 0xFFFE;

    private readonly IAssetLoader _assets;

    /// <summary>Initializes the loader with the asset loader that reads the files.</summary>
    /// <param name="assets">The asset loader that opens the sound files.</param>
    /// <exception cref="ArgumentNullException">The asset loader is null.</exception>
    public WavSoundLoader(IAssetLoader assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        _assets = assets;
    }

    /// <inheritdoc />
    public SoundData Load(string relativePath)
    {
        byte[] bytes = ReadAll(relativePath);

        if (bytes.Length < RiffHeaderSize || Tag(bytes, 0) != "RIFF" || Tag(bytes, 8) != "WAVE")
        {
            throw new InvalidDataException($"The file '{relativePath}' is not a RIFF WAVE sound.");
        }

        int format = 0;
        int channels = 0;
        int sampleRate = 0;
        int bitsPerSample = 0;
        int dataOffset = -1;
        int dataLength = 0;

        int offset = RiffHeaderSize;
        while (offset + ChunkHeaderSize <= bytes.Length)
        {
            string tag = Tag(bytes, offset);
            int length = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4));
            int body = offset + ChunkHeaderSize;

            if (body > bytes.Length)
            {
                throw new InvalidDataException($"The chunk '{tag}' of '{relativePath}' starts past the end of the file.");
            }

            if (tag == "fmt ")
            {
                if (length < FormatBodySize)
                {
                    throw new InvalidDataException($"The format chunk of '{relativePath}' holds {length} bytes, but a WAVE header needs at least {FormatBodySize}.");
                }

                format = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(body));
                channels = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(body + 2));
                sampleRate = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(body + 4));
                bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(body + 14));

                if (format == ExtensibleFormat && length >= 26)
                {
                    // WAVE_FORMAT_EXTENSIBLE keeps the real format in the first bytes of its sub format field.
                    format = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(body + 24));
                }
            }
            else if (tag == "data")
            {
                dataOffset = body;
                dataLength = length;
            }

            offset = body + length + (length % 2);
        }

        if (dataOffset < 0)
        {
            throw new InvalidDataException($"The file '{relativePath}' has no data chunk.");
        }

        if (format is not (PcmFormat or FloatFormat))
        {
            throw new InvalidDataException($"The file '{relativePath}' uses audio format {format}, but only PCM and IEEE float samples are supported.");
        }

        if (channels is < 1 or > 2)
        {
            throw new InvalidDataException($"The file '{relativePath}' holds {channels} channels, but only mono and stereo are supported.");
        }

        if (sampleRate <= 0)
        {
            throw new InvalidDataException($"The file '{relativePath}' reports a sample rate of {sampleRate}, which is not usable.");
        }

        int available = Math.Min(dataLength, bytes.Length - dataOffset);
        int frameSize = bitsPerSample / 8 * channels;
        int wholeFrames = frameSize > 0 ? available - (available % frameSize) : 0;

        return new SoundData(sampleRate, channels, Convert(bytes.AsSpan(dataOffset, wholeFrames), bitsPerSample, format));
    }

    /// <summary>Converts the samples of the data chunk to signed 16-bit values.</summary>
    private static short[] Convert(ReadOnlySpan<byte> data, int bitsPerSample, int format)
    {
        if (format == FloatFormat)
        {
            return ConvertFloat(data);
        }

        return bitsPerSample switch
        {
            8 => ConvertEightBit(data),
            16 => ConvertSixteenBit(data),
            24 => ConvertTwentyFourBit(data),
            32 => ConvertThirtyTwoBit(data),
            _ => throw new InvalidDataException($"Samples of {bitsPerSample} bits are not supported."),
        };
    }

    /// <summary>Converts unsigned 8-bit samples, whose middle is 128, to signed 16-bit ones.</summary>
    private static short[] ConvertEightBit(ReadOnlySpan<byte> data)
    {
        var samples = new short[data.Length];

        for (int index = 0; index < samples.Length; index++)
        {
            samples[index] = (short)((data[index] - 128) << 8);
        }

        return samples;
    }

    private static short[] ConvertSixteenBit(ReadOnlySpan<byte> data)
    {
        var samples = new short[data.Length / 2];

        for (int index = 0; index < samples.Length; index++)
        {
            samples[index] = BinaryPrimitives.ReadInt16LittleEndian(data[(index * 2)..]);
        }

        return samples;
    }

    /// <summary>Keeps the high 16 bits of a 24-bit sample, which keeps the sign of the value.</summary>
    private static short[] ConvertTwentyFourBit(ReadOnlySpan<byte> data)
    {
        var samples = new short[data.Length / 3];

        for (int index = 0; index < samples.Length; index++)
        {
            int offset = index * 3;
            int value = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
            samples[index] = (short)((value << 8) >> 16);
        }

        return samples;
    }

    private static short[] ConvertThirtyTwoBit(ReadOnlySpan<byte> data)
    {
        var samples = new short[data.Length / 4];

        for (int index = 0; index < samples.Length; index++)
        {
            samples[index] = (short)(BinaryPrimitives.ReadInt32LittleEndian(data[(index * 4)..]) >> 16);
        }

        return samples;
    }

    /// <summary>Converts float samples, which run from minus one to one, to signed 16-bit ones.</summary>
    private static short[] ConvertFloat(ReadOnlySpan<byte> data)
    {
        var samples = new short[data.Length / 4];

        for (int index = 0; index < samples.Length; index++)
        {
            float value = BinaryPrimitives.ReadSingleLittleEndian(data[(index * 4)..]);
            samples[index] = (short)Math.Clamp(value * short.MaxValue, short.MinValue, short.MaxValue);
        }

        return samples;
    }

    /// <summary>Reads the whole file, because a sound is small and the chunks refer to each other by offset.</summary>
    private byte[] ReadAll(string relativePath)
    {
        using Stream stream = _assets.OpenRead(relativePath);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>Reads the four-byte tag of a chunk.</summary>
    private static string Tag(byte[] bytes, int offset) => Encoding.ASCII.GetString(bytes, offset, 4);
}
