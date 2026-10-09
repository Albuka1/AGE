using Age.Assets;
using Age.Content.Sheets;
using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Age.Tests;

public sealed class SpriteSheetServiceTests : IDisposable
{
    private readonly string _root;
    private readonly RecordingLogger<SpriteSheetService> _logger = new();
    private readonly FakeImageLoader _images = new();
    private readonly TextureService _textures;
    private readonly SpriteSheetService _sheets;

    public SpriteSheetServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "age-sheets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        var assets = new NullAssetLoader();
        assets.Initialize(_root);

        _textures = new TextureService(_images, new FakeRenderer());
        _sheets = new SpriteSheetService(assets, _textures, _logger);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void SpriteSheetService_Load_ReadsTheDocumentOnceAndKeepsIt()
    {
        Write("goblin.yml", SpriteSheetReaderTests.Document);
        Write("goblin.bmp", string.Empty);

        SpriteSheet first = _sheets.Load("goblin.yml");
        SpriteSheet second = _sheets.Load("goblin.yml");

        first.Should().BeSameAs(second);
        _sheets.Count.Should().Be(1);
        first.Image.Should().Be("Textures/Entities/goblin.bmp");
    }

    [Fact]
    public void SpriteSheetService_Resolve_AnswersTheCellOfTheFrameAndTheImage()
    {
        Write("goblin.yml", SpriteSheetReaderTests.Document);
        Write("goblin.bmp", string.Empty);

        SpriteRegion region = _sheets.Resolve("goblin.yml", "attack", frame: 2);

        region.Texture.Id.Should().NotBe(0, "the image of the sheet was uploaded");
        region.Cell.Should().Be(new Vector2(16f, 16f), "which is the size a sprite takes when it names none");
        region.Source.X.Should().Be(0.5f);
        region.Source.Y.Should().Be(2f / 3f);
        region.Source.Width.Should().Be(0.25f);
        _sheets.Missing.Should().BeEmpty();
    }

    [Fact]
    public void SpriteSheetService_Resolve_ADocumentThatIsNotThere_AnswersThePlaceholderAndReportsItOnce()
    {
        SpriteRegion first = _sheets.Resolve("Nowhere/goblin.yml", "idle", frame: 0);
        SpriteRegion second = _sheets.Resolve("Nowhere/goblin.yml", "idle", frame: 0);

        first.Texture.Should().Be(_textures.Error, "a sheet that is not there is drawn as the placeholder");
        second.Should().Be(first);
        _sheets.Missing.Should().Equal("Nowhere/goblin.yml");
        _logger.Records.Should().ContainSingle(record => record.Level == LogLevel.Error, "a broken sheet is reported once rather than on every frame");
    }

    [Fact]
    public void SpriteSheetService_Resolve_AStateTheSheetDoesNotDeclare_AnswersThePlaceholder()
    {
        Write("goblin.yml", SpriteSheetReaderTests.Document);

        SpriteRegion region = _sheets.Resolve("goblin.yml", "jump", frame: 0);

        region.Texture.Should().Be(_textures.Error);
        _sheets.Missing.Should().Equal("goblin.yml:jump");
        _logger.Records.Should().ContainSingle(record => record.Message.Contains("declares no state 'jump'", StringComparison.Ordinal));
    }

    [Fact]
    public void SpriteSheetService_Resolve_AFrameOutsideTheState_DrawsTheFirstFrameAndReportsIt()
    {
        Write("goblin.yml", SpriteSheetReaderTests.Document);
        Write("goblin.bmp", string.Empty);

        SpriteRegion region = _sheets.Resolve("goblin.yml", "attack", frame: 9);

        region.Source.X.Should().Be(0f, "the first frame of the state is drawn instead of nothing");
        _sheets.Missing.Should().Equal("goblin.yml:attack:9");
    }

    [Fact]
    public void SpriteSheetService_TryState_AnswersWhetherASheetPlaysAState()
    {
        Write("goblin.yml", SpriteSheetReaderTests.Document);

        _sheets.TryState("goblin.yml", "walk", out SpriteSheetState? walk).Should().BeTrue();
        walk!.Frames.Should().Be(4);
        _sheets.TryState("goblin.yml", "jump", out SpriteSheetState? missing).Should().BeFalse();
        missing.Should().BeNull();
    }

    [Fact]
    public void SpriteSheetService_Load_ADocumentThatIsNotASheet_ThrowsAndSaysWhyAgain()
    {
        Write("list.yml", "- a list rather than a sheet\n");

        Action first = () => _sheets.Load("list.yml");
        first.Should().Throw<SpriteSheetException>().WithMessage("*list.yml*");

        Action second = () => _sheets.Load("list.yml");
        second.Should().Throw<SpriteSheetException>().WithMessage("*a set of fields*", "the document is read once, and what was wrong with it is kept");
    }

    [Fact]
    public void SpriteSheetService_Resolve_AnImageThatIsNotThere_DrawsThePlaceholderOfTheTextures()
    {
        Write("goblin.yml", SpriteSheetReaderTests.Document);
        _images.Missing.Add("Textures/Entities/goblin.bmp");

        SpriteRegion region = _sheets.Resolve("goblin.yml", "idle", frame: 0);

        region.Texture.Should().Be(_textures.Error, "the image of a sheet goes through the texture service, which answers a missing image with the placeholder");
        _textures.MissingCount.Should().Be(1);
        _textures.Missing.Should().Contain("Textures/Entities/goblin.bmp");
    }

    private void Write(string relativePath, string text)
    {
        string path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    /// <summary>Keeps what was logged, which is how a test reads the record of a sheet that could not be resolved.</summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Records { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Records.Add((logLevel, formatter(state, exception)));
    }

    private sealed class FakeImageLoader : IImageLoader
    {
        public HashSet<string> Missing { get; } = new(StringComparer.Ordinal);

        public ImageData Load(string relativePath) =>
            Missing.Contains(relativePath)
                ? throw new FileNotFoundException($"There is no image at '{relativePath}'.")
                : new ImageData(2, 2, new byte[16]);
    }

    /// <summary>A renderer that only uploads textures, which is what the texture service asks of one.</summary>
    private sealed class FakeRenderer : IRenderer
    {
        private int _created;

        public Vector2 ViewportSize => new(1280f, 720f);

        public void Attach(IWindowService window)
        {
        }

        public void SetCamera(Camera2D camera)
        {
        }

        public void BeginFrame(bool clear)
        {
        }

        public void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color, float rotation = 0f)
        {
        }

        public void DrawTextureRegion(TextureHandle texture, Rect source, Vector2 position, Vector2 size, Color color, float rotation = 0f)
        {
        }

        public void DrawRectangle(Rect rect, Color color)
        {
        }

        public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color)
        {
        }

        public void EndFrame()
        {
        }

        public TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height) => new(++_created);

        public void ReleaseTexture(TextureHandle texture)
        {
        }

        public void Dispose()
        {
        }
    }
}
