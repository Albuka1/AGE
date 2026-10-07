namespace Age.Assets;

/// <summary>
/// Converts the float samples that the compressed decoders produce into the signed 16-bit values that
/// <see cref="SoundData"/> holds.
/// </summary>
internal static class SoundSamples
{
    /// <summary>Converts float samples, which run from minus one to one, to signed 16-bit ones.</summary>
    /// <param name="samples">The samples to convert.</param>
    /// <returns>The samples at the depth of <see cref="SoundData"/>.</returns>
    internal static short[] FromFloat(ReadOnlySpan<float> samples)
    {
        var converted = new short[samples.Length];

        for (int index = 0; index < converted.Length; index++)
        {
            converted[index] = (short)Math.Clamp(samples[index] * short.MaxValue, short.MinValue, short.MaxValue);
        }

        return converted;
    }
}
