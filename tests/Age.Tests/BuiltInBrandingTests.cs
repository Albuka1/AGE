using System.Reflection;
using Age.Rendering;
using FluentAssertions;
using StbImageSharp;
using Xunit;

namespace Age.Tests;

public sealed class BuiltInBrandingTests
{
    [Fact]
    public void BuiltInBranding_Assembly_EmbedsTheIconsAndTheLogo()
    {
        Assembly assembly = typeof(SplashScreen).Assembly;

        assembly.GetManifestResourceNames().Should().Contain(
        [
            "Age.Rendering.Resources.icon-20.png",
            "Age.Rendering.Resources.icon-40.png",
            "Age.Rendering.Resources.icon-60.png",
            "Age.Rendering.Resources.logo-320.png",
        ]);
    }

    [Theory]
    [InlineData("icon-20.png", 20)]
    [InlineData("icon-40.png", 40)]
    [InlineData("icon-60.png", 60)]
    [InlineData("logo-320.png", 320)]
    public void BuiltInBranding_Asset_DecodesAsASquareRgbaImage(string fileName, int size)
    {
        using Stream stream = typeof(SplashScreen).Assembly.GetManifestResourceStream($"Age.Rendering.Resources.{fileName}")!;
        ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha)!;

        image.Width.Should().Be(size);
        image.Height.Should().Be(size);
        image.Data.Should().HaveCount(size * size * 4);
    }

    [Fact]
    public void BuiltInBranding_Logo_HasATransparentBackground()
    {
        using Stream stream = typeof(SplashScreen).Assembly.GetManifestResourceStream("Age.Rendering.Resources.logo-320.png")!;
        ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha)!;

        image.Data[3].Should().Be(0, "the top-left corner of the logo has no background");
        image.Data[(4 * (319 * 320)) + 3].Should().Be(0, "the bottom-left corner of the logo has no background");
    }
}
