using StbImageSharp;

namespace Age.Assets;

/// <summary>
/// Decodes images with StbImageSharp. It reads through <see cref="IAssetLoader"/>, so every path stays inside the game root.
/// </summary>
public sealed class StbImageLoader : IImageLoader
{
    private readonly IAssetLoader _assets;

    /// <summary>Initializes the loader with the asset loader that reads the files.</summary>
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
