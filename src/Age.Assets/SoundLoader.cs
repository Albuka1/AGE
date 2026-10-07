namespace Age.Assets;

/// <summary>
/// The default <see cref="ISoundLoader"/>. It reads the header of the file and hands it to the loader of that format,
/// so a game does not have to know which decoder a sound needs.
/// </summary>
/// <remarks>
/// The header decides, not the file name: a RIFF WAVE, MPEG audio with or without an ID3 tag, and Ogg Vorbis are
/// recognised. Each of those loaders reads the file through <see cref="IAssetLoader"/>, so every path stays inside the
/// game root.
/// </remarks>
/// <example>
/// <code>
/// SoundData music = sounds.Load("music/theme.ogg");
/// SoundData click = sounds.Load("sfx/click.wav");
/// </code>
/// </example>
public sealed class SoundLoader : ISoundLoader
{
    /// <summary>The number of header bytes that are enough to tell the format of a sound.</summary>
    private const int HeaderSize = 12;

    private readonly IAssetLoader _assets;
    private readonly WavSoundLoader _wav;
    private readonly Mp3SoundLoader _mp3;
    private readonly OggSoundLoader _ogg;

    /// <summary>Initializes the loader with the asset loader and the loader of every supported format.</summary>
    /// <param name="assets">The asset loader that opens the files to read their header.</param>
    /// <param name="wav">The loader of RIFF WAVE sounds.</param>
    /// <param name="mp3">The loader of MPEG audio sounds.</param>
    /// <param name="ogg">The loader of Ogg Vorbis sounds.</param>
    /// <exception cref="ArgumentNullException">One of the arguments is null.</exception>
    public SoundLoader(IAssetLoader assets, WavSoundLoader wav, Mp3SoundLoader mp3, OggSoundLoader ogg)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(wav);
        ArgumentNullException.ThrowIfNull(mp3);
        ArgumentNullException.ThrowIfNull(ogg);
        _assets = assets;
        _wav = wav;
        _mp3 = mp3;
        _ogg = ogg;
    }

    /// <inheritdoc />
    public SoundData Load(string relativePath)
    {
        SoundFileFormat format;

        using (Stream stream = _assets.OpenRead(relativePath))
        {
            format = Detect(ReadHeader(stream));
        }

        return format switch
        {
            SoundFileFormat.Wave => _wav.Load(relativePath),
            SoundFileFormat.Mp3 => _mp3.Load(relativePath),
            SoundFileFormat.Ogg => _ogg.Load(relativePath),
            _ => throw new InvalidDataException($"The file '{relativePath}' is not a sound in a supported format. WAVE, MPEG audio and Ogg Vorbis are."),
        };
    }

    /// <summary>Reads the first bytes of a stream, which are enough to tell the format.</summary>
    private static byte[] ReadHeader(Stream stream)
    {
        var header = new byte[HeaderSize];
        int read = stream.ReadAtLeast(header, HeaderSize, throwOnEndOfStream: false);

        return read == HeaderSize ? header : header[..read];
    }

    /// <summary>Tells the format of a sound from its header.</summary>
    private static SoundFileFormat Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= HeaderSize && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            return SoundFileFormat.Wave;
        }

        if (header.Length >= 4 && header[..4].SequenceEqual("OggS"u8))
        {
            return SoundFileFormat.Ogg;
        }

        // MPEG audio carries an ID3 tag or starts with a frame sync, whose first eleven bits are set.
        if ((header.Length >= 3 && header[..3].SequenceEqual("ID3"u8)) || (header.Length >= 2 && header[0] == 0xFF && (header[1] & 0xE0) == 0xE0))
        {
            return SoundFileFormat.Mp3;
        }

        return SoundFileFormat.Unknown;
    }

    /// <summary>The formats that this loader can tell apart.</summary>
    private enum SoundFileFormat
    {
        Unknown,
        Wave,
        Mp3,
        Ogg,
    }
}
