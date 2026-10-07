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

    /// <summary>The offset of the size field of the RIFF header, which closes the form when it is added to its value.</summary>
    private const int RiffFormOffset = 8;

    /// <summary>The length of a format chunk that carries a WAVE_FORMAT_EXTENSIBLE header, in bytes.</summary>
    private const int ExtensibleFormatSize = 40;

    /// <summary>The offset of the sub format GUID inside a WAVE_FORMAT_EXTENSIBLE header.</summary>
    private const int SubFormatOffset = 24;

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

        // The declared size closes the RIFF form, so anything behind it belongs to something else, such as a second
        // form in a file that was appended to. A form that is truncated is still read up to the end of the file.
        long declaredSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4));
        int formEnd = (int)Math.Min(bytes.Length, RiffFormOffset + declaredSize);

        int format = 0;
        int channels = 0;
        int sampleRate = 0;
        int bitsPerSample = 0;
        int dataOffset = -1;
        int dataLength = 0;

        int offset = RiffHeaderSize;

        while (offset + ChunkHeaderSize <= formEnd)
        {
            string tag = Tag(bytes, offset);
            long length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4));
            long body = offset + ChunkHeaderSize;
            long remaining = formEnd - body;

            if (remaining < 0)
            {
                throw new InvalidDataException($"The chunk '{tag}' of '{relativePath}' starts past the end of the RIFF form.");
            }

            if (tag == "fmt ")
            {
                if (length < FormatBodySize || length > remaining)
                {
                    throw new InvalidDataException($"The format chunk of '{relativePath}' claims {length} bytes, but the file holds {remaining}.");
                }

                format = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + ChunkHeaderSize));
                channels = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + ChunkHeaderSize + 2));
                sampleRate = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + ChunkHeaderSize + 4));
                bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + ChunkHeaderSize + 14));

                if (format == ExtensibleFormat)
                {
                    if (length < ExtensibleFormatSize)
                    {
                        throw new InvalidDataException($"The format chunk of '{relativePath}' is extensible but holds {length} bytes instead of the {ExtensibleFormatSize} that its sub format needs.");
                    }

                    format = ReadExtensibleFormat(bytes.AsSpan(offset + ChunkHeaderSize + SubFormatOffset));
                }
            }
            else if (tag == "data")
            {
                dataOffset = offset + ChunkHeaderSize;

                // A truncated file is read up to its end instead of being refused: the samples that are there are the
                // samples the game can play.
                dataLength = (int)Math.Min(length, remaining);
            }
            else if (length > remaining)
            {
                throw new InvalidDataException($"The chunk '{tag}' of '{relativePath}' claims {length} bytes, but the file holds {remaining}.");
            }

            offset = (int)Math.Min(body + length + (length % 2), int.MaxValue);
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

    /// <summary>Reads the format of a WAVE_FORMAT_EXTENSIBLE sub format, or the extensible tag when its GUID is not one of the standard ones.</summary>
    /// <param name="guid">The sixteen bytes of the sub format GUID.</param>
    /// <returns>The format tag of the sub format, or <see cref="ExtensibleFormat"/> when the GUID is not a standard one.</returns>
    /// <remarks>
    /// The standard sub formats are <c>XXXXXXXX-0000-0010-8000-00AA00389B71</c>, so only their first four bytes differ and
    /// they hold the tag of the format: PCM is one and IEEE float is three. Comparing the rest of the GUID is what keeps
    /// a sub format that only looks like one of them from being decoded as if it were.
    /// </remarks>
    private static int ReadExtensibleFormat(ReadOnlySpan<byte> guid)
    {
        ReadOnlySpan<byte> tail = [0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71];

        return guid.Slice(4, tail.Length).SequenceEqual(tail)
            ? (int)BinaryPrimitives.ReadUInt32LittleEndian(guid)
            : ExtensibleFormat;
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
