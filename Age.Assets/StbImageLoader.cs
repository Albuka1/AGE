using StbImageSharp;

namespace Age.Assets;

/// <summary>
/// The default <see cref="IImageLoader"/>. Decoding is done by StbImageSharp, a managed port of the stb_image decoder
/// that needs no native dependency, and the file itself is read through <see cref="IAssetLoader"/>, so every path stays
/// inside the game root. Register another <see cref="IImageLoader"/> when a different decoder is needed.
/// </summary>
public sealed class StbImageLoader : IImageLoader
{
    private readonly IAssetLoader _assets;

    /// <summary>Initializes the loader with the asset loader that reads the files.</summary>
    /// <param name="assets">The asset loader that opens the image files.</param>
    /// <exception cref="ArgumentNullException">The asset loader is null.</exception>
    public StbImageLoader(IAssetLoader assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        _assets = assets;
    }

    /// <inheritdoc />
    public ImageData Load(string relativePath)
    {
        using Stream stream = _assets.OpenRead(relativePath);

        ImageResult? image;
        try
        {
            image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException($"The file '{relativePath}' is not a supported image.", exception);
        }

        return image is null
            ? throw new InvalidDataException($"The file '{relativePath}' is not a supported image.")
            : new ImageData(image.Width, image.Height, image.Data);
    }
}
