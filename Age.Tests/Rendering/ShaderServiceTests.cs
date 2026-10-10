using Age.Assets;
using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class ShaderServiceTests : IDisposable
{
    private const string FragmentPath = "Shaders/displacement.frag";
    private const string VertexPath = "Shaders/displacement.vert";

    private readonly string _root;
    private readonly NullAssetLoader _assets = new();
    private readonly RecordingRenderer _renderer = new();
    private readonly ShaderService _shaders;

    public ShaderServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "age-shaders-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "Shaders"));
        File.WriteAllText(Path.Combine(_root, "Shaders", "displacement.frag"), "void main()\n{\n    COLOR = vec4(1.0);\n}\n");
        File.WriteAllText(Path.Combine(_root, "Shaders", "displacement.vert"), "void main()\n{\n    gl_Position = vec4(0.0);\n}\n");
        _assets.Initialize(_root);
        _shaders = new ShaderService(_assets, _renderer);
    }

    public void Dispose()
    {
        _shaders.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void ShaderService_Load_CompilesOncePerPairOfStages()
    {
        ShaderHandle first = _shaders.Load(FragmentPath);
        ShaderHandle again = _shaders.Load(FragmentPath);
        ShaderHandle other = _shaders.Load(FragmentPath, VertexPath);

        again.Should().Be(first);
        other.Should().NotBe(first);
        _shaders.Count.Should().Be(2);
        _renderer.Compiled.Should().HaveCount(2);
    }

    [Fact]
    public void ShaderService_Load_ReadsTheStagesUnderTheHeaderOfTheEngine()
    {
        _shaders.Load(FragmentPath);

        string vertex = _renderer.Compiled[0].Vertex;
        string fragment = _renderer.Compiled[0].Fragment;

        vertex.Should().StartWith("#version 330 core");
        vertex.Should().Contain("gl_Position = vec4(aPosition, 0.0, 1.0) * uProjection;", "a fragment shader is drawn with the vertex stage of the engine");
        fragment.Should().StartWith("#version 330 core");
        fragment.Should().Contain("uniform sampler2D uTexture;");
        fragment.Should().Contain("COLOR = vec4(1.0);", "what a game wrote follows the header");
    }

    [Fact]
    public void ShaderService_Load_AStageThatIsNotThere_IsRefused()
    {
        Action load = () => _shaders.Load("Shaders/nowhere.frag");

        load.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void ShaderService_Unload_ReleasesTheProgram()
    {
        ShaderHandle shader = _shaders.Load(FragmentPath);

        _shaders.IsAlive(shader).Should().BeTrue();
        _shaders.Unload(shader).Should().BeTrue();
        _shaders.IsAlive(shader).Should().BeFalse();
        _shaders.Unload(shader).Should().BeFalse();
        _renderer.Released.Should().ContainSingle();
    }

    [Fact]
    public void ShaderService_Load_AfterAnotherAttachment_CompilesTheStagesAgain()
    {
        ShaderHandle first = _shaders.Load(FragmentPath);

        // Another window is another device, so the program of the first one is gone with it and the numbers of it are handed
        // out again: a handle of the attachment before names nothing of the device that is there now.
        _renderer.DeviceGeneration = 1;

        ShaderHandle again = _shaders.Load(FragmentPath);

        _renderer.Compiled.Should().HaveCount(2, "the stages are compiled for the device that is there now");
        again.Should().NotBe(first);
        again.Generation.Should().Be(1);
        _shaders.Count.Should().Be(1, "the entry of the attachment before is forgotten rather than kept beside the new one");
        _shaders.IsAlive(first).Should().BeFalse();
        _shaders.IsAlive(again).Should().BeTrue();
    }

    [Fact]
    public void ShaderService_Unload_AProgramOfAnEarlierAttachment_IsNotReleasedByNumber()
    {
        ShaderHandle first = _shaders.Load(FragmentPath);
        _renderer.DeviceGeneration = 1;

        // The device that is there now gave the number of that program to a program of its own, so releasing it by number would
        // delete the wrong one: the entry is forgotten and the device is left alone.
        _shaders.Unload(first).Should().BeTrue();
        _renderer.Released.Should().BeEmpty();

        ShaderHandle again = _shaders.Load(FragmentPath);
        _shaders.Unload(again).Should().BeTrue();
        _renderer.Released.Should().ContainSingle().Which.Should().Be(again.Program, "a program of the attachment that is there is released");
    }

    [Fact]
    public void ShaderService_TheHeaderIsWhatACompileErrorCountsFrom()
    {
        string[] lines = ShaderSource.Fragment("void main() { }").Split('\n');

        ShaderSource.FragmentHeaderLines.Should().BeGreaterThan(0);
        ShaderSource.VertexHeaderLines.Should().BeGreaterThan(0);
        lines[ShaderSource.FragmentHeaderLines].Should().Be("void main() { }", "a line that a compiler reports counts the lines of the header as well");
    }

    [Fact]
    public void ShaderService_Load_TheHeaderOfAStage_NamesTheSurfaceThatTheFrameIsDrawnInto()
    {
        _shaders.Load(FragmentPath);

        string vertex = _renderer.Compiled[0].Vertex;
        string fragment = _renderer.Compiled[0].Fragment;

        // A shader of a game post-processes the frame that it is drawn into, so the header of a stage names that surface, the
        // size of it in pixels, and the flip that the copy of it needs.
        fragment.Should().Contain("uniform sampler2D uScreen;");
        fragment.Should().Contain("#define SCREEN_TEXTURE uScreen");
        fragment.Should().Contain("#define SCREEN_SIZE uScreenSize");
        fragment.Should().Contain("#define SCREEN_PIXEL_SIZE (1.0 / uScreenSize)");
        fragment.Should().Contain("#define SCREEN_UV vec2(vTexCoord.x, 1.0 - vTexCoord.y)");
        fragment.Should().Contain("vec4 sampleScreen(vec2 uv)");
        vertex.Should().Contain("uniform vec2 uScreenSize;", "the stage that places a quad measures a pixel of the surface as well");
    }

    private sealed class RecordingRenderer : IRenderer
    {
        public List<(string Vertex, string Fragment)> Compiled { get; } = [];

        public List<uint> Released { get; } = [];

        public uint DeviceGeneration { get; set; }

        private uint _next;

        public Vector2 ViewportSize => new(1280f, 720f);

        public uint CompileShader(string vertexSource, string fragmentSource)
        {
            Compiled.Add((vertexSource, fragmentSource));

            return ++_next;
        }

        public void ReleaseShader(uint program) => Released.Add(program);

        public void Attach(IWindowService window) { }

        public void SetCamera(Camera2D camera) { }

        public void BeginFrame(bool clear) { }

        public void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color, float rotation = 0f) { }

        public void DrawTextureRegion(TextureHandle texture, Rect source, Vector2 position, Vector2 size, Color color, float rotation = 0f) { }

        public void DrawRectangle(Rect rect, Color color) { }

        public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color) { }

        public void EndFrame() { }

        public TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height) => new(1);

        public void ReleaseTexture(TextureHandle texture) { }

        public void Dispose() { }
    }
}
