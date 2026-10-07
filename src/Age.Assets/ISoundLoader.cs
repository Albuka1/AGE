namespace Age.Assets;

/// <summary>
/// Decodes sounds into sample data. The supported formats come from the implementation and the file itself is read
/// through <see cref="IAssetLoader"/>, so every path stays inside the game root.
/// </summary>
/// <example>
/// <code>
/// SoundData click = sounds.Load("sfx/click.wav");
/// Console.WriteLine($"{click.Duration.TotalSeconds:0.00} s");
/// </code>
/// </example>
public interface ISoundLoader
{
    /// <summary>Decodes the sound at the given path relative to the game root.</summary>
    /// <param name="relativePath">The path of the sound, relative to the game root.</param>
    /// <returns>The decoded samples together with their rate and channel count, as signed 16-bit values.</returns>
    /// <exception cref="InvalidDataException">The file is not a sound in one of the supported formats.</exception>
    /// <exception cref="FileNotFoundException">No file exists at the given path.</exception>
    /// <remarks>
    /// <see cref="SoundLoader"/> is the default and the only public one: it reads the header of the file and hands it to
    /// the decoder of that format, which covers RIFF WAVE, MPEG audio and Ogg Vorbis. Those decoders are implementation
    /// details, and each of them converts the samples to 16-bit values. A file whose format is not supported is reported
    /// as <see cref="InvalidDataException"/>.
    /// </remarks>
    SoundData Load(string relativePath);
}
