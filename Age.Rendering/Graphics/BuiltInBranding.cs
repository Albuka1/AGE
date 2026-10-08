using Age.Assets;
using StbImageSharp;

namespace Age.Rendering;

/// <summary>
/// The branding the engine ships with: the application icon, drawn on a background, and the logo that
/// <see cref="SplashScreen"/> shows. Both are embedded PNG files under Resources, so a game gets them without shipping
/// content of its own and without a file system access at startup.
/// </summary>
/// <remarks>
/// The images are decoded on first use and kept for the lifetime of the process. Decoding goes through
/// StbImageSharp, the same decoder that <see cref="IImageLoader"/> wraps for game assets.
/// </remarks>
internal static class BuiltInBranding
{
    private static ImageData[]? _icons;
    private static ImageData? _logo;

    /// <summary>Gets the icon of the engine, smallest image first, so the operating system can pick a size.</summary>
    internal static ImageData[] Icons => _icons ??=
    [
        Load("Textures/Icons/icon-20.png"),
        Load("Textures/Icons/icon-40.png"),
        Load("Textures/Icons/icon-60.png"),
    ];

    /// <summary>Gets the logo of the engine, which has no background and is 320 by 320 pixels.</summary>
    internal static ImageData Logo => _logo ??= Load("Textures/Logo/logo-320.png");

    /// <summary>Decodes one of the embedded images.</summary>
    /// <param name="path">The path of the image relative to the shared Resources folder.</param>
    /// <returns>The decoded pixels.</returns>
    /// <exception cref="InvalidOperationException">The assembly does not embed the image.</exception>
    /// <exception cref="InvalidDataException">The embedded file is not an image in a supported format.</exception>
    private static ImageData Load(string path)
    {
        string resourceName = $"Age.Rendering.Resources.{path.Replace('/', '.')}";
        using Stream stream = typeof(BuiltInBranding).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The engine does not embed the branding asset '{resourceName}'.");

        ImageResult? image;
        try
        {
            image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException($"The embedded branding asset '{resourceName}' is not a supported image.", exception);
        }

        return image is null
            ? throw new InvalidDataException($"The embedded branding asset '{resourceName}' is not a supported image.")
            : new ImageData(image.Width, image.Height, image.Data);
    }
}
