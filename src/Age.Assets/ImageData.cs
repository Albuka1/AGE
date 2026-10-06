namespace Age.Assets;

/// <summary>
/// Decoded image data, stored as tightly packed RGBA bytes in row-major order.
/// </summary>
public sealed class ImageData
{
    /// <summary>Initializes decoded image data. The buffer must hold four bytes for every pixel.</summary>
    public ImageData(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(pixels);

        if (pixels.LongLength != (long)width * height * 4)
        {
            throw new ArgumentException(
                $"The buffer holds {pixels.LongLength} bytes, but {width} x {height} RGBA pixels need {(long)width * height * 4}.",
                nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    /// <summary>Gets the image width, in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the image height, in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets the pixel data as RGBA bytes, four per pixel, starting at the top-left corner.</summary>
    public byte[] Pixels { get; }
}
