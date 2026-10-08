namespace Age.Assets;

/// <summary>
/// Decodes images into RGBA pixel data. The supported formats come from StbImageSharp and the file itself is read
/// through <see cref="IAssetLoader"/>, so every path stays inside the game root.
/// </summary>
/// <example>
/// <code>
/// ImageData logo = images.Load("ui/logo.png");
/// Console.WriteLine($"{logo.Width} x {logo.Height}");
/// </code>
/// </example>
public interface IImageLoader
{
    /// <summary>Decodes the image at the given path relative to the game root.</summary>
    /// <param name="relativePath">The path of the image, relative to the game root.</param>
    /// <returns>The decoded pixels together with the size of the image.</returns>
    /// <exception cref="InvalidDataException">The file is not an image in one of the supported formats.</exception>
    /// <exception cref="FileNotFoundException">No file exists at the given path.</exception>
    /// <remarks>
    /// StbImageSharp reads PNG, JPEG, BMP, TGA and GIF. The pixels come back as RGBA bytes in row-major order with the
    /// origin at the top-left corner, which is what a texture upload expects.
    /// </remarks>
    ImageData Load(string relativePath);
}
