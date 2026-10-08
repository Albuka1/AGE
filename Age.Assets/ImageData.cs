namespace Age.Assets;

/// <summary>
/// Decoded image data, stored as tightly packed RGBA bytes in row-major order.
/// </summary>
/// <example>
/// <code>
/// ImageData logo = images.Load("ui/logo.png");
///
/// int x = 4;
/// int y = 3;
/// int offset = ((y * logo.Width) + x) * 4;
/// Color pixel = new(logo.Pixels[offset], logo.Pixels[offset + 1], logo.Pixels[offset + 2], logo.Pixels[offset + 3]);
/// </code>
/// </example>
public sealed class ImageData
{
    /// <summary>Initializes decoded image data.</summary>
    /// <param name="width">The width of the image, in pixels. Must be greater than zero.</param>
    /// <param name="height">The height of the image, in pixels. Must be greater than zero.</param>
    /// <param name="pixels">The pixel data, four RGBA bytes per pixel. The buffer must be exactly width * height * 4 long.</param>
    /// <exception cref="ArgumentOutOfRangeException">Width or height is zero or negative.</exception>
    /// <exception cref="ArgumentNullException">The pixel buffer is null.</exception>
    /// <exception cref="ArgumentException">The pixel buffer does not hold four bytes for every pixel.</exception>
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
    /// <remarks>
    /// Byte <c>((y * Width) + x) * 4</c> is the red channel of the pixel at <c>(x, y)</c>; green, blue and alpha follow
    /// it. The buffer is row-major, so a row of the image is <c>Width * 4</c> bytes long.
    /// </remarks>
    public byte[] Pixels { get; }
}
