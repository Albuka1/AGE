using NVorbis;

namespace Age.Assets;

/// <summary>
/// An <see cref="ISoundLoader"/> for Ogg Vorbis, which is the compressed format that games use for music. Decoding is
/// done by NVorbis, a managed decoder that needs no native dependency, and the file itself is read through
/// <see cref="IAssetLoader"/>, so every path stays inside the game root.
/// </summary>
/// <remarks>
/// Decoding runs as long as the stream reports samples, so the whole sound is held in memory as 16-bit samples.
/// </remarks>
public sealed class OggSoundLoader : ISoundLoader
{
    /// <summary>The number of frames that one read asks the decoder for.</summary>
    private const int FramesPerRead = 4096;

    private readonly IAssetLoader _assets;

    /// <summary>Initializes the loader with the asset loader that reads the files.</summary>
    /// <param name="assets">The asset loader that opens the sound files.</param>
    /// <exception cref="ArgumentNullException">The asset loader is null.</exception>
    public OggSoundLoader(IAssetLoader assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        _assets = assets;
    }

    /// <inheritdoc />
    public SoundData Load(string relativePath)
    {
        using Stream stream = _assets.OpenRead(relativePath);

        VorbisReader reader;
        try
        {
            reader = new VorbisReader(stream, closeOnDispose: false);
        }
        catch (Exception exception) when (exception is not InvalidDataException)
        {
            throw new InvalidDataException($"The file '{relativePath}' is not an Ogg Vorbis sound.", exception);
        }

        using (reader)
        {
            int channels = reader.Channels;
            int sampleRate = reader.SampleRate;
            var samples = new List<short>();
            var buffer = new float[FramesPerRead * Math.Max(channels, 1)];
            int read;

            while ((read = reader.ReadSamples(buffer, 0, buffer.Length)) > 0)
            {
                samples.AddRange(SoundSamples.FromFloat(buffer.AsSpan(0, read)));
            }

            if (samples.Count == 0 || channels is < 1 or > 2 || sampleRate <= 0)
            {
                throw new InvalidDataException($"The file '{relativePath}' holds no Ogg Vorbis sound that can be played.");
            }

            return new SoundData(sampleRate, channels, [.. samples]);
        }
    }
}
