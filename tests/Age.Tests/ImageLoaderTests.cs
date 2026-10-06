using System.Text;
using Age.Assets;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class ImageLoaderTests : IDisposable
{
    private const string OnePixelPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private readonly string _root;
    private readonly StbImageLoader _loader;

    public ImageLoaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "age-images-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        var assets = new NullAssetLoader();
        assets.Initialize(_root);
        _loader = new StbImageLoader(assets);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void ImageLoader_LoadPng_ReturnsRgbaPixels()
    {
        Write("dot.png", Convert.FromBase64String(OnePixelPng));

        ImageData image = _loader.Load("dot.png");

        image.Width.Should().Be(1);
        image.Height.Should().Be(1);
        image.Pixels.Should().HaveCount(4);
    }

    [Fact]
    public void ImageLoader_LoadBmp_ConvertsBgrToRgba()
    {
        (int R, int G, int B)[] pixels =
        [
            (0x10, 0x20, 0x30), (0x40, 0x50, 0x60),
            (0x70, 0x80, 0x90), (0xA0, 0xB0, 0xC0),
        ];

        Write("quad.bmp", CreateBmp(width: 2, height: 2, pixels));

        ImageData image = _loader.Load("quad.bmp");

        byte[] expected =
        [
            0x10, 0x20, 0x30, 0xFF,
            0x40, 0x50, 0x60, 0xFF,
            0x70, 0x80, 0x90, 0xFF,
            0xA0, 0xB0, 0xC0, 0xFF,
        ];

        image.Width.Should().Be(2);
        image.Height.Should().Be(2);
        image.Pixels.Should().Equal(expected);
    }

    [Fact]
    public void ImageLoader_LoadMissingFile_ThrowsFileNotFoundException()
    {
        Action act = () => _loader.Load("missing.png");

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void ImageLoader_LoadNotAnImage_ThrowsInvalidDataException()
    {
        Write("broken.png", "this is not an image");

        Action act = () => _loader.Load("broken.png");

        act.Should().Throw<InvalidDataException>();
    }

    [Theory]
    [InlineData(2, 2, 4)]
    [InlineData(2, 2, 17)]
    [InlineData(1, 1, 3)]
    public void ImageData_PixelBufferSizeMismatch_ThrowsArgumentException(int width, int height, int length)
    {
        Action act = () => new ImageData(width, height, new byte[length]);

        act.Should().Throw<ArgumentException>();
    }

    private static byte[] CreateBmp(int width, int height, (int R, int G, int B)[] pixels)
    {
        int stride = (((width * 3) + 3) / 4) * 4;
        byte[] bytes = new byte[54 + (stride * height)];

        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        WriteInt32(bytes, 2, bytes.Length);
        WriteInt32(bytes, 10, 54);
        WriteInt32(bytes, 14, 40);
        WriteInt32(bytes, 18, width);
        WriteInt32(bytes, 22, height);
        WriteInt16(bytes, 26, 1);
        WriteInt16(bytes, 28, 24);
        WriteInt32(bytes, 34, stride * height);

        for (int row = 0; row < height; row++)
        {
            int source = (height - 1 - row) * width;
            int target = 54 + (row * stride);

            for (int column = 0; column < width; column++)
            {
                (int r, int g, int b) = pixels[source + column];
                bytes[target + (column * 3)] = (byte)b;
                bytes[target + (column * 3) + 1] = (byte)g;
                bytes[target + (column * 3) + 2] = (byte)r;
            }
        }

        return bytes;
    }

    private static void WriteInt16(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteInt32(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
    }

    private void Write(string relativePath, byte[] content)
    {
        string path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    private void Write(string relativePath, string content) => Write(relativePath, Encoding.UTF8.GetBytes(content));
}
