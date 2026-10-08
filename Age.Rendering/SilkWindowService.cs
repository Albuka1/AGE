using System.Runtime.Versioning;
using Age.Assets;
using Silk.NET.Core;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Age.Rendering;

/// <summary>
/// Creates and owns a Silk.NET window with an OpenGL 3.3 core profile context.
/// </summary>
public sealed class SilkWindowService : IWindowService
{
    private readonly List<Win32Icon.IconBitmaps> _icons = [];
    private IWindow? _window;

    /// <inheritdoc />
    public IWindow Window => _window ?? throw new InvalidOperationException("The window has not been created. Call Create first.");

    /// <inheritdoc />
    public void Create(int width, int height, string title)
    {
        if (_window is not null)
        {
            return;
        }

        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(width, height),
            Title = title,
            API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3)),
        };

        _window = Silk.NET.Windowing.Window.Create(options);
        _window.Initialize();
        ApplyIcons(BuiltInBranding.Icons);
    }

    /// <inheritdoc />
    public void SetIcon(ReadOnlySpan<byte> pixels, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        long required = (long)width * height * 4;
        if (required > int.MaxValue || pixels.Length != required)
        {
            throw new ArgumentException($"The buffer holds {pixels.Length} bytes, but {width} x {height} RGBA pixels need {required} bytes, which is not a supported icon size.", nameof(pixels));
        }

        ApplyIcons([new ImageData(width, height, pixels.ToArray())]);
    }

    /// <inheritdoc />
    public void Close()
    {
        _window?.Close();

        if (OperatingSystem.IsWindows())
        {
            ReleaseIcons();
        }
    }

    /// <summary>Gives the images to the window, so the smallest of them becomes the small icon and the largest the large one.</summary>
    /// <param name="images">The images to offer, ordered by size.</param>
    /// <remarks>
    /// Windows needs the icon to be built from the pixels, because the window backend that Silk.NET uses drops it. Other
    /// platforms go through that backend. The images are ordered by size, so the first and the last one fit the two
    /// icon sizes that Windows keeps.
    /// </remarks>
    private void ApplyIcons(ImageData[] images)
    {
        if (!OperatingSystem.IsWindows())
        {
            RawImage[] raw = [.. images.Select(ToIcon)];
            Window.SetWindowIcon(raw);
            return;
        }

        if (Window.Native?.Win32 is not (nint handle, _, _))
        {
            return;
        }

        Win32Icon.IconBitmaps large = Win32Icon.Create(images[^1].Width, images[^1].Height, images[^1].Pixels);
        Win32Icon.IconBitmaps small = Win32Icon.Create(images[0].Width, images[0].Height, images[0].Pixels);

        SetIcon(handle, large, true);
        SetIcon(handle, small, false);
    }

    /// <summary>Sends one icon to the window, keeps it alive while the window uses it, and releases the icon it replaced.</summary>
    [SupportedOSPlatform("windows")]
    private void SetIcon(nint handle, Win32Icon.IconBitmaps icon, bool large)
    {
        if (icon.Icon == 0)
        {
            return;
        }

        // The window answers with the icon it showed before, which is one of ours when an earlier call set it: release
        // that one, so a game that replaces its icon while it runs does not pile icons up. A handle the window owned
        // itself is left alone.
        nint replaced = Win32Icon.Apply(handle, icon.Icon, large);
        ReleaseIcon(replaced);
        _icons.Add(icon);
    }

    /// <summary>Releases the remembered icon that carries the handle, when it is one of ours.</summary>
    [SupportedOSPlatform("windows")]
    private void ReleaseIcon(nint replaced)
    {
        for (int index = 0; index < _icons.Count; index++)
        {
            if (_icons[index].Icon != replaced)
            {
                continue;
            }

            Win32Icon.Release(_icons[index]);
            _icons.RemoveAt(index);
            return;
        }
    }

    /// <summary>Releases the icons of the window.</summary>
    [SupportedOSPlatform("windows")]
    private void ReleaseIcons()
    {
        foreach (Win32Icon.IconBitmaps icon in _icons)
        {
            Win32Icon.Release(icon);
        }

        _icons.Clear();
    }

    /// <summary>Converts decoded pixels into the image the window offers to the operating system.</summary>
    private static RawImage ToIcon(ImageData image) => new(image.Width, image.Height, image.Pixels);
}
