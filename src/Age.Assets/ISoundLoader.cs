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
    /// <see cref="WavSoundLoader"/> reads RIFF WAVE files: PCM and IEEE float samples of 8, 16, 24 and 32 bits, in mono
    /// or stereo, which it converts to 16-bit values. Samples of a file that cannot be converted, such as a compressed
    /// codec, are reported as <see cref="InvalidDataException"/>.
    /// </remarks>
    SoundData Load(string relativePath);
}
