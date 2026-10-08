using NLayer;

namespace Age.Assets;

/// <summary>
/// An <see cref="ISoundLoader"/> for MPEG audio, which covers the MP3 files that games usually ship. Decoding is done
/// by NLayer, a managed decoder that needs no native dependency, and the file itself is read through
/// <see cref="IAssetLoader"/>, so every path stays inside the game root.
/// </summary>
/// <remarks>
/// Decoding runs as long as the stream reports samples, so the whole sound is held in memory as 16-bit samples: this is
/// what a game wants for a short effect or a music track that plays often.
/// </remarks>
/// <summary>
/// The decoder of MPEG audio sounds, which <see cref="SoundLoader"/> picks when a file carries an ID3 tag or starts with
/// a frame sync. Decoding is done by NLayer, a managed decoder that needs no native dependency.
/// </summary>
internal sealed class Mp3SoundLoader : ISoundLoader
{
    /// <summary>The number of frames that one read asks the decoder for.</summary>
    private const int FramesPerRead = 4096;

    private readonly IAssetLoader _assets;

    /// <summary>Initializes the loader with the asset loader that reads the files.</summary>
    /// <param name="assets">The asset loader that opens the sound files.</param>
    /// <exception cref="ArgumentNullException">The asset loader is null.</exception>
    public Mp3SoundLoader(IAssetLoader assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        _assets = assets;
    }

    /// <inheritdoc />
    public SoundData Load(string relativePath)
    {
        using Stream stream = _assets.OpenRead(relativePath);

        MpegFile file;
        try
        {
            file = new MpegFile(stream);
        }
        catch (Exception exception) when (exception is not InvalidDataException)
        {
            throw new InvalidDataException($"The file '{relativePath}' is not MPEG audio.", exception);
        }

        using (file)
        {
            int channels = file.Channels;
            int sampleRate = file.SampleRate;
            var samples = new List<short>();
            var buffer = new float[FramesPerRead * Math.Max(channels, 1)];
            int read;

            while ((read = file.ReadSamples(buffer, 0, buffer.Length)) > 0)
            {
                samples.AddRange(SoundSamples.FromFloat(buffer.AsSpan(0, read)));
            }

            if (samples.Count == 0 || channels is < 1 or > 2 || sampleRate <= 0)
            {
                throw new InvalidDataException($"The file '{relativePath}' holds no MPEG audio that can be played.");
            }

            return new SoundData(sampleRate, channels, [.. samples]);
        }
    }
}
