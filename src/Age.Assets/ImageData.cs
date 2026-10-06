namespace Age.Assets;

/// <summary>
/// Decoded image data, stored as tightly packed RGBA bytes in row-major order.
/// </summary>
public sealed class ImageData
{
    /// <summary>Initializes decoded image data.</summary>
    public ImageData(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(pixels);
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
