namespace Age.Assets;

/// <summary>
/// Decodes images into RGBA pixel data.
/// </summary>
public interface IImageLoader
{
    /// <summary>
    /// Decodes the image at the given path relative to the game root. Every format that StbImageSharp reads is
    /// supported, including PNG, JPEG, BMP, TGA and GIF. Throws InvalidDataException when the file is not a
    /// supported image.
    /// </summary>
    ImageData Load(string relativePath);
}
