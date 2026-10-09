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
    public void ShaderService_TheHeaderIsWhatACompileErrorCountsFrom()
    {
        string[] lines = ShaderSource.Fragment("void main() { }").Split('\n');

        ShaderSource.FragmentHeaderLines.Should().BeGreaterThan(0);
        ShaderSource.VertexHeaderLines.Should().BeGreaterThan(0);
        lines[ShaderSource.FragmentHeaderLines].Should().Be("void main() { }", "a line that a compiler reports counts the lines of the header as well");
    }

    private sealed class RecordingRenderer : IRenderer
    {
        public List<(string Vertex, string Fragment)> Compiled { get; } = [];

        public List<uint> Released { get; } = [];

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
