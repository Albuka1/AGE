using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Age.Rendering;

/// <summary>
/// Builds a Windows icon out of RGBA pixels and gives it to a window through <c>WM_SETICON</c>.
/// </summary>
/// <remarks>
/// <see cref="IWindowService"/> asks the window backend for its icon through <c>IWindow.SetWindowIcon</c> as well, but
/// the backend that Silk.NET uses does not hand the icon to the window on Windows, so the window would keep the
/// default one. The calls below are what that backend leaves out: they build an icon from the pixels and send it to
/// the window handle.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class Win32Icon
{
    private const int WmSetIcon = 0x0080;
    private const int IconSmall = 0;
    private const int IconBig = 1;
    private const uint DibRgbColors = 0;
    private const uint BitFields = 3;
    private const uint Srgb = 0x73524742;

    /// <summary>Builds an icon from RGBA pixels.</summary>
    /// <param name="width">The width of the image, in pixels.</param>
    /// <param name="height">The height of the image, in pixels.</param>
    /// <param name="rgba">The pixels, four RGBA bytes each, in row-major order from the top-left corner.</param>
    /// <returns>The icon and the bitmaps it was built from. Release all of them with <see cref="Release"/>.</returns>
    internal static IconBitmaps Create(int width, int height, ReadOnlySpan<byte> rgba)
    {
        nint screen = GetDC(0);
        nint memory = CreateCompatibleDC(screen);
        nint color = 0;
        nint mask = 0;

        try
        {
            var info = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),

                    // A negative height asks for a top-down bitmap, so its rows arrive in the order of the pixels.
                    Width = width,
                    Height = -height,
                    Planes = 1,
                    BitCount = 32,
                    Compression = BitFields,
                    SizeImage = (uint)(width * height * 4),
                    RedMask = 0x00FF0000,
                    GreenMask = 0x0000FF00,
                    BlueMask = 0x000000FF,
                    AlphaMask = 0xFF000000,
                    ColorSpace = Srgb,
                },
            };

            color = CreateDIBSection(memory, ref info, DibRgbColors, out nint bits, 0, 0);
            mask = color == 0 ? 0 : CreateBitmap(width, height, 1, 1, 0);

            if (color == 0 || mask == 0 || bits == 0)
            {
                return default;
            }

            CopyRgba(rgba, bits, width * height);

            var iconInfo = new IconInfo { IsIcon = true, Color = color, Mask = mask };
            return new IconBitmaps(CreateIconIndirect(ref iconInfo), color, mask);
        }
        finally
        {
            DeleteDC(memory);
            ReleaseDC(0, screen);
        }
    }

    /// <summary>Hands the icon to the window, which shows it in its title bar and in the taskbar.</summary>
    /// <param name="window">The window handle.</param>
    /// <param name="icon">The icon to show.</param>
    /// <param name="large">Whether the icon is the large one instead of the small one.</param>
    internal static void Apply(nint window, nint icon, bool large) =>
        _ = SendMessage(window, WmSetIcon, large ? IconBig : IconSmall, icon);

    /// <summary>Releases an icon and the bitmaps it was built from.</summary>
    internal static void Release(IconBitmaps bitmaps)
    {
        if (bitmaps.Icon != 0)
        {
            _ = DestroyIcon(bitmaps.Icon);
        }

        if (bitmaps.Mask != 0)
        {
            _ = DeleteObject(bitmaps.Mask);
        }

        if (bitmaps.Color != 0)
        {
            _ = DeleteObject(bitmaps.Color);
        }
    }

    /// <summary>Copies RGBA pixels into the device-independent bitmap, which stores them as BGRA.</summary>
    private static unsafe void CopyRgba(ReadOnlySpan<byte> rgba, nint destination, int count)
    {
        byte* target = (byte*)destination;

        for (int pixel = 0; pixel < count; pixel++)
        {
            int offset = pixel * 4;
            target[offset] = rgba[offset + 2];
            target[offset + 1] = rgba[offset + 1];
            target[offset + 2] = rgba[offset];
            target[offset + 3] = rgba[offset + 3];
        }
    }

    /// <summary>The icon and the bitmaps it was created from. All three stay alive until they are released together.</summary>
    internal readonly record struct IconBitmaps(nint Icon, nint Color, nint Mask);

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    /// <summary>
    /// A BITMAPV5HEADER: the version that carries the channel masks, which is what keeps the alpha channel of the
    /// pixels alive. Its fields must stay in this order, because Windows reads them by their offsets in the memory.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int ResolutionX;
        public int ResolutionY;
        public uint ColorsUsed;
        public uint ColorsImportant;
        public uint RedMask;
        public uint GreenMask;
        public uint BlueMask;
        public uint AlphaMask;
        public uint ColorSpace;
        public int EndpointRedX;
        public int EndpointRedY;
        public int EndpointRedZ;
        public int EndpointGreenX;
        public int EndpointGreenY;
        public int EndpointGreenZ;
        public int EndpointBlueX;
        public int EndpointBlueY;
        public int EndpointBlueZ;
        public uint GammaRed;
        public uint GammaGreen;
        public uint GammaBlue;
        public uint Intent;
        public uint ProfileData;
        public uint ProfileSize;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsIcon;

        public int HotspotX;
        public int HotspotY;
        public nint Mask;
        public nint Color;
    }

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint window, nint deviceContext);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint deviceContext);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(nint deviceContext);

    [DllImport("gdi32.dll")]
    private static extern nint CreateDIBSection(nint deviceContext, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);

    [DllImport("gdi32.dll")]
    private static extern nint CreateBitmap(int width, int height, uint planes, uint bitCount, nint bits);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint handle);

    [DllImport("user32.dll")]
    private static extern nint CreateIconIndirect(ref IconInfo info);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint icon);
}
