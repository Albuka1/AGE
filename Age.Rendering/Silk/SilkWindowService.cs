using System.Runtime.Versioning;
using Age.Assets;
using Silk.NET.Core;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Age.Rendering;

/// <summary>
/// Creates and owns a Silk.NET window with an OpenGL 3.3 core profile context, and holds the state of the display that a game
/// changes through <see cref="IWindowService"/>.
/// </summary>
public sealed class SilkWindowService : IWindowService
{
    private readonly List<Win32Icon.IconBitmaps> _icons = [];
    private IWindow? _window;
    private string _title = string.Empty;
    private bool _fullscreen;
    private bool _vsync = true;
    private int _fpsLimit;

    /// <inheritdoc />
    public IWindow Window => _window ?? throw new InvalidOperationException("The window has not been created. Call Create first.");

    /// <inheritdoc />
    public string Title
    {
        get => _title;
        set
        {
            // The value is kept even before the window exists, so a game that sets the title of its run and then creates the window
            // opens it with the title it asked for rather than with the one the call to Create passes.
            _title = value ?? string.Empty;

            if (_window is not null)
            {
                _window.Title = _title;
            }
        }
    }

    /// <inheritdoc />
    public bool Fullscreen
    {
        get => _fullscreen;
        set
        {
            _fullscreen = value;

            if (_window is not null)
            {
                _window.WindowState = value ? WindowState.Fullscreen : WindowState.Normal;
            }
        }
    }

    /// <inheritdoc />
    public bool VSync
    {
        get => _vsync;
        set
        {
            _vsync = value;

            if (_window is not null)
            {
                _window.VSync = value;
            }
        }
    }

    /// <inheritdoc />
    public int FpsLimit
    {
        get => _fpsLimit;
        set => _fpsLimit = value < 0 ? 0 : value;
    }

    /// <inheritdoc />
    /// <remarks>
    /// A window that is on a monitor answers the rate the window manager reports for that monitor, which is what a display that
    /// runs at 144 hertz answers as 144. A window that is not on a monitor yet, and a platform that does not report a rate,
    /// answer zero.
    /// </remarks>
    public int RefreshRate
    {
        get
        {
            IMonitor? monitor = _window?.Monitor;
            return monitor?.VideoMode.RefreshRate ?? 0;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Every property of this service writes itself to the window, so the call writes nothing that did not change: it exists for a
    /// game that wrote several of them through a settings file and would rather apply them together. Nothing is written before the
    /// window exists, because a game may build its settings before it opens one.
    /// </remarks>
    public void Apply()
    {
        if (_window is null)
        {
            return;
        }

        _window.Title = _title;
        _window.WindowState = _fullscreen ? WindowState.Fullscreen : WindowState.Normal;
        _window.VSync = _vsync;
    }

    /// <inheritdoc />
    public void Create(int width, int height, string title)
    {
        if (_window is not null)
        {
            return;
        }

        // The title the game set through the property wins over the one this call passes, because it is the one the game last wrote.
        if (string.IsNullOrEmpty(_title))
        {
            _title = title ?? string.Empty;
        }

        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(width, height),
            Title = _title,
            API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3)),

            // The loop of the game drives the frames and polls the events itself, so the window is not event-driven: an event-driven
            // window waits for events inside DoEvents, which is what a loop that already decides when to advance the frame does not
            // want, and which blocks the whole game once a second window shares the one platform of the process.
            IsEventDriven = false,
            VSync = _vsync,
        };

        _window = Silk.NET.Windowing.Window.Create(options);
        _window.Initialize();

        // The state that was asked for before the window existed is written once here, so a game that sets fullscreen in its
        // settings and then creates the window opens it in fullscreen rather than in a window that becomes full screen later.
        _window.WindowState = _fullscreen ? WindowState.Fullscreen : WindowState.Normal;

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
