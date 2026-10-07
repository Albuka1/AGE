using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Age.Assets;
using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class AssetLoaderTests : IDisposable
{
    private const string Text = "hello age\nпривет";

    private readonly string _root;
    private readonly NullAssetLoader _loader = new();

    public AssetLoaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "age-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _loader.Initialize(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AssetLoader_InitializeWithMissingGameRoot_ThrowsArgumentException(string? gameRoot)
    {
        var loader = new NullAssetLoader();

        Action act = () => loader.Initialize(gameRoot!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AssetLoader_UseBeforeInitialize_ThrowsInvalidOperationException()
    {
        var loader = new NullAssetLoader();

        Action exists = () => loader.Exists("sprite.txt");
        Action open = () => loader.OpenRead("sprite.txt");
        Action load = () => loader.Load<string>("sprite.txt");

        exists.Should().Throw<InvalidOperationException>();
        open.Should().Throw<InvalidOperationException>();
        load.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AssetLoader_ExistsAndOpenRead_UseTheGameRoot()
    {
        Write("sprite.txt", Text);

        _loader.Exists("sprite.txt").Should().BeTrue();
        _loader.Exists("missing.txt").Should().BeFalse();

        using Stream stream = _loader.OpenRead("sprite.txt");
        using var reader = new StreamReader(stream, Encoding.UTF8);

        reader.ReadToEnd().Should().Be(Text);
    }

    [Fact]
    public void AssetLoader_ExistsInSubdirectory_IsTrue()
    {
        Write("art/tiles/floor.txt", Text);

        _loader.Exists("art/tiles/floor.txt").Should().BeTrue();
    }

    [Fact]
    public void AssetLoader_EmptyPath_ThrowsInvalidOperationException()
    {
        Action act = () => _loader.Exists(string.Empty);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AssetLoader_AbsolutePath_ThrowsInvalidOperationException()
    {
        string absolute = Path.Combine(_root, "sprite.txt");

        Action act = () => _loader.Exists(absolute);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("art/../../outside.txt")]
    public void AssetLoader_EscapingPath_ThrowsInvalidOperationException(string relativePath)
    {
        Action act = () => _loader.Load<string>(relativePath);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AssetLoader_LoadString_ReadsUtf8Text()
    {
        Write("greeting.txt", Text);

        _loader.Load<string>("greeting.txt").Should().Be(Text);
    }

    [Fact]
    public void AssetLoader_LoadBytes_ReturnsRawBytes()
    {
        byte[] content = [0x01, 0x02, 0xFF];
        Write("blob.bin", content);

        _loader.Load<byte[]>("blob.bin").Should().Equal(content);
    }

    [Fact]
    public void AssetLoader_LoadJson_PopulatesComponentFields()
    {
        Write(
            "spawn.json",
            """
            {
              // comments, trailing commas and camel case are accepted
              "position": { "x": 120.5, "y": 64 },
              "scale": { "x": 2, "y": 2 },
            }
            """);

        TransformComponent transform = _loader.Load<TransformComponent>("spawn.json");

        transform.Position.X.Should().Be(120.5f);
        transform.Position.Y.Should().Be(64f);
        transform.Scale.Should().Be(new Vector2(2f, 2f));
    }

    [Fact]
    public void AssetLoader_LoadMissingFile_ThrowsFileNotFoundException()
    {
        Action act = () => _loader.Load<string>("missing.txt");

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void AssetLoader_LoadInvalidJson_ThrowsJsonException()
    {
        Write("broken.json", "{ \"position\": ");

        Action act = () => _loader.Load<TransformComponent>("broken.json");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void AssetLoader_LinkPointingOutsideTheRoot_ThrowsInvalidOperationException()
    {
        string outside = Path.Combine(Path.GetTempPath(), "age-assets-outside-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(outside, Text);

        try
        {
            File.CreateSymbolicLink(Path.Combine(_root, "link.txt"), outside);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            File.Delete(outside);
            Assert.Skip("Creating symbolic links is not permitted here.");
        }

        try
        {
            Action act = () => _loader.Exists("link.txt");

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public void AssetLoader_LoadWithAGeneratedContract_ReadsJsonWithoutReflection()
    {
        Write("settings.json", """{ "Name": "age", "Count": 7 }""");

        AssetSettings settings = _loader.Load("settings.json", AssetJsonContext.Default.AssetSettings);

        settings.Name.Should().Be("age");
        settings.Count.Should().Be(7);
    }

    private void Write(string relativePath, string content) => Write(relativePath, Encoding.UTF8.GetBytes(content));

    private void Write(string relativePath, byte[] content)
    {
        string path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }
}

/// <summary>The contract of the settings that the loader test reads, generated at compile time instead of through reflection.</summary>
[JsonSourceGenerationOptions(IncludeFields = true)]
[JsonSerializable(typeof(AssetSettings))]
internal sealed partial class AssetJsonContext : JsonSerializerContext;

/// <summary>A payload of the loader test. A struct with fields, like the components of the engine.</summary>
internal struct AssetSettings
{
    public string? Name;

    public int Count;
}
