using System.Reflection;
using Age.Rendering;
using FluentAssertions;
using StbImageSharp;
using Xunit;

namespace Age.Tests;

public sealed class BuiltInBrandingTests
{
    /// <summary>The logical name under which the assembly embeds the logo, which every branding image follows.</summary>
    private const string LogoResource = "Age.Rendering.Resources.Textures.Logo.logo-320.png";

    [Fact]
    public void BuiltInBranding_Assembly_EmbedsTheIconsAndTheLogo()
    {
        Assembly assembly = typeof(SplashScreen).Assembly;

        assembly.GetManifestResourceNames().Should().Contain(
        [
            "Age.Rendering.Resources.Textures.Icons.icon-20.png",
            "Age.Rendering.Resources.Textures.Icons.icon-40.png",
            "Age.Rendering.Resources.Textures.Icons.icon-60.png",
            LogoResource,
        ]);
    }

    [Theory]
    [InlineData("Age.Rendering.Resources.Textures.Icons.icon-20.png", 20)]
    [InlineData("Age.Rendering.Resources.Textures.Icons.icon-40.png", 40)]
    [InlineData("Age.Rendering.Resources.Textures.Icons.icon-60.png", 60)]
    [InlineData(LogoResource, 320)]
    public void BuiltInBranding_Asset_DecodesAsASquareRgbaImage(string resourceName, int size)
    {
        using Stream stream = typeof(SplashScreen).Assembly.GetManifestResourceStream(resourceName)!;
        ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha)!;

        image.Width.Should().Be(size);
        image.Height.Should().Be(size);
        image.Data.Should().HaveCount(size * size * 4);
    }

    [Fact]
    public void BuiltInBranding_Logo_HasATransparentBackground()
    {
        using Stream stream = typeof(SplashScreen).Assembly.GetManifestResourceStream(LogoResource)!;
        ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha)!;

        image.Data[3].Should().Be(0, "the top-left corner of the logo has no background");
        image.Data[(4 * (319 * 320)) + 3].Should().Be(0, "the bottom-left corner of the logo has no background");
    }
}
