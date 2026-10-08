namespace Age.Assets;

/// <summary>
/// Decoded sound data, stored as signed 16-bit samples in the format they were recorded in.
/// </summary>
/// <example>
/// <code>
/// SoundData click = sounds.Load("Audio/Effects/click.wav");
///
/// int frame = 10;
/// short left = click.Samples[frame * click.Channels];
/// </code>
/// </example>
/// <remarks>
/// Samples are interleaved, so a frame holds one sample per channel in a row, and a file that was recorded in another
/// depth is converted to 16 bits by the loader: this is the depth that the audio device takes without conversion.
/// </remarks>
public sealed class SoundData
{
    /// <summary>Initializes decoded sound data.</summary>
    /// <param name="sampleRate">The number of frames per second, which must be greater than zero.</param>
    /// <param name="channels">The number of channels, one for mono and two for stereo.</param>
    /// <param name="samples">The interleaved samples. The length must be a whole number of frames.</param>
    /// <exception cref="ArgumentOutOfRangeException">The sample rate is zero or negative, or the channel count is not one or two.</exception>
    /// <exception cref="ArgumentNullException">The sample buffer is null.</exception>
    /// <exception cref="ArgumentException">The sample buffer does not hold a whole number of frames.</exception>
    public SoundData(int sampleRate, int channels, short[] samples)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfLessThan(channels, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(channels, 2);
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Length % channels != 0)
        {
            throw new ArgumentException(
                $"The buffer holds {samples.Length} samples, which is not a whole number of {channels}-channel frames.",
                nameof(samples));
        }

        SampleRate = sampleRate;
        Channels = channels;
        Samples = samples;
    }

    /// <summary>Gets the number of frames per second.</summary>
    public int SampleRate { get; }

    /// <summary>Gets the number of channels, one for mono and two for stereo.</summary>
    public int Channels { get; }

    /// <summary>Gets the samples as signed 16-bit values, interleaved per frame.</summary>
    public short[] Samples { get; }

    /// <summary>Gets the number of frames, which is the length of the sound divided by its channels.</summary>
    public int FrameCount => Samples.Length / Channels;

    /// <summary>Gets how long the sound lasts.</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds((double)FrameCount / SampleRate);
}
