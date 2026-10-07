using Age.Assets;
using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class FontServiceTests : IDisposable
{
    private const string FontPath = "fonts/Cousine-Regular.ttf";

    private readonly string _root;
    private readonly NullAssetLoader _assets = new();
    private readonly RecordingRenderer _renderer = new();
    private readonly FontService _fonts;

    public FontServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "age-fonts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "fonts"));
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "fonts", "Cousine-Regular.ttf"),
            Path.Combine(_root, "fonts", "Cousine-Regular.ttf"));
        _assets.Initialize(_root);
        _fonts = new FontService(_assets, _renderer);
    }

    public void Dispose()
    {
        _fonts.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void FontService_Load_BakesOneAtlasAndCachesByPathAndHeight()
    {
        FontHandle first = _fonts.Load(FontPath, 24f);
        FontHandle again = _fonts.Load(FontPath, 24f);
        FontHandle other = _fonts.Load(FontPath, 48f);

        again.Should().Be(first);
        other.Should().NotBe(first);
        _fonts.Count.Should().Be(2);
        _renderer.Created.Should().HaveCount(2, "one atlas per height");
        _renderer.Created.Should().OnlyContain(atlas => atlas.Width > 0 && atlas.Height > 0);
        first.Atlas.Should().Be(_renderer.Created[0].Id);
    }

    [Fact]
    public void FontService_Measure_AddsUpTheAdvances()
    {
        FontHandle font = _fonts.Load(FontPath, 24f);

        Vector2 size = _fonts.Measure(font, "AGE");

        size.X.Should().BeGreaterThan(0f);
        size.Y.Should().BeGreaterThan(0f);
        _fonts.Measure(font, string.Empty).Should().Be(new Vector2(0f, size.Y));
        _fonts.Measure(font, "AGE").Should().Be(size);
    }

    [Fact]
    public void FontService_Draw_IssuesOneRegionPerGlyphThatHasInk()
    {
        FontHandle font = _fonts.Load(FontPath, 24f);

        _fonts.Draw(font, "A A", new Vector2(10f, 20f), Color.Red);

        _renderer.Regions.Should().HaveCount(2, "a space has no ink to draw");
        _renderer.Regions[0].Texture.Should().Be(font.Atlas);
        _renderer.Regions[0].Color.Should().Be(Color.Red);
        _renderer.Regions[0].Source.Width.Should().BeGreaterThan(0f);
        _renderer.Regions[0].Size.X.Should().BeGreaterThan(0f);
        _renderer.Regions[0].Size.Y.Should().BeGreaterThan(0f);
        _renderer.Regions[1].Position.X.Should().BeGreaterThan(_renderer.Regions[0].Position.X, "the pen advanced");
    }

    [Fact]
    public void FontService_CharacterOutsideTheRange_AdvancesLikeASpace()
    {
        FontHandle font = _fonts.Load(FontPath, 24f);

        float space = _fonts.Measure(font, "A ").X;

        _fonts.Measure(font, "A\u00FF").X.Should().Be(space);
        _fonts.Measure(font, "A\n").X.Should().Be(space);
    }

    [Fact]
    public void FontService_Unload_ReleasesTheAtlasAndMakesTheHandleStale()
    {
        FontHandle font = _fonts.Load(FontPath, 24f);
        int atlas = font.Atlas;

        _fonts.Unload(font).Should().BeTrue();

        _renderer.Released.Should().Equal(atlas);
        _fonts.Count.Should().Be(0);
        _fonts.IsAlive(font).Should().BeFalse();
        _fonts.Unload(font).Should().BeFalse();
        FluentActions.Invoking(() => _fonts.Measure(font, "A")).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => _fonts.Draw(font, "A", Vector2.Zero, Color.White)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FontService_LoadAgainAfterAnUnload_BakesANewAtlas()
    {
        FontHandle first = _fonts.Load(FontPath, 24f);
        _fonts.Unload(first).Should().BeTrue();

        FontHandle second = _fonts.Load(FontPath, 24f);

        second.Should().NotBe(first);
        _renderer.Created.Should().HaveCount(2);
    }

    [Fact]
    public void FontService_Dispose_ReleasesEveryAtlas()
    {
        _fonts.Load(FontPath, 16f);
        _fonts.Load(FontPath, 32f);

        _fonts.Dispose();

        _renderer.Released.Should().HaveCount(2);
        _fonts.Count.Should().Be(0);
    }

    [Fact]
    public void FontService_HandleThatWasNeverLoaded_Throws()
    {
        FluentActions.Invoking(() => _fonts.Measure(default, "A")).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => _fonts.Draw(default, "A", Vector2.Zero, Color.White)).Should().Throw<InvalidOperationException>();
        _fonts.IsAlive(default).Should().BeFalse();
        _fonts.Unload(default).Should().BeFalse();
    }

    [Fact]
    public void FontService_HeightThatIsNotUsable_Throws()
    {
        FluentActions.Invoking(() => _fonts.Load(FontPath, 0f)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => _fonts.Load(FontPath, -8f)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => _fonts.Load(FontPath, float.NaN)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => _fonts.Load(FontPath, float.PositiveInfinity)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => _fonts.Load("   ", 16f)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FontService_MissingFileOrBytesThatHoldNoFont_Throws()
    {
        File.WriteAllBytes(Path.Combine(_root, "fonts", "not-a-font.ttf"), new byte[2048]);

        FluentActions.Invoking(() => _fonts.Load("fonts/missing.ttf", 16f)).Should().Throw<FileNotFoundException>();
        FluentActions.Invoking(() => _fonts.Load("fonts/not-a-font.ttf", 16f)).Should().Throw<ArgumentException>();
    }

    private sealed class RecordingRenderer : IRenderer
    {
        public List<(int Id, int Width, int Height)> Created { get; } = [];
        public List<int> Released { get; } = [];
        public List<Region> Regions { get; } = [];

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

        public void DrawTextureRegion(TextureHandle texture, Rect source, Vector2 position, Vector2 size, Color color, float rotation = 0f) =>
            Regions.Add(new Region(texture.Id, source, position, size, color));

        public void DrawRectangle(Rect rect, Color color)
        {
        }

        public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color)
        {
        }

        public void EndFrame()
        {
        }

        public TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height)
        {
            int id = Created.Count + 1;
            Created.Add((id, width, height));
            return new TextureHandle(id);
        }

        public void ReleaseTexture(TextureHandle texture) => Released.Add(texture.Id);

        public void Dispose()
        {
        }
    }

    private readonly record struct Region(int Texture, Rect Source, Vector2 Position, Vector2 Size, Color Color);
}
