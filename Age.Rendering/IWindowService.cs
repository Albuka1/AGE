using Silk.NET.Windowing;

namespace Age.Rendering;

/// <summary>
/// Owns the application window.
/// </summary>
public interface IWindowService
{
    /// <summary>Gets the underlying window.</summary>
    IWindow Window { get; }

    /// <summary>Creates the window. It must be called before the game loop runs.</summary>
    /// <remarks>The window opens with the icon of the engine. Replace it with <see cref="SetIcon"/>.</remarks>
    void Create(int width, int height, string title);

    /// <summary>Replaces the window icon with the given image.</summary>
    /// <param name="pixels">The pixels, four RGBA bytes each, in row-major order from the top-left corner.</param>
    /// <param name="width">The width of the image, in pixels.</param>
    /// <param name="height">The height of the image, in pixels.</param>
    /// <remarks>
    /// The image becomes the icon in the title bar and, on Windows, in the taskbar, so a square image of 32 or 64
    /// pixels per side suits it. Decode a file into pixels with <see cref="T:Age.Assets.IImageLoader"/> first: this call
    /// takes pixels, not a path, which keeps it free of any file format.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Width or height is zero or negative.</exception>
    /// <exception cref="ArgumentException">The buffer does not hold four bytes for every pixel, or the image is too large to be an icon.</exception>
    /// <exception cref="InvalidOperationException">The window has not been created.</exception>
    void SetIcon(ReadOnlySpan<byte> pixels, int width, int height);

    /// <summary>Closes the window.</summary>
    void Close();
}
