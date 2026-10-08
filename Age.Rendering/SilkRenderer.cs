using Age.Core;
using Microsoft.Extensions.Logging;
using Silk.NET.OpenGL;

namespace Age.Rendering;

/// <summary>
/// An OpenGL 3.3 core renderer that draws everything through one private shader program.
/// </summary>
/// <remarks>
/// The vertex shader receives a position, a texture coordinate and a color, and projects the position with the matrix
/// the renderer composes from the camera and the viewport. The fragment shader is a pass-through: it outputs the
/// interpolated color, or multiplies it with the sampled texel when a texture is bound. Text and the built-in font atlas
/// are drawn as quads like everything else, so no fixed-function drawing is used. User-facing shaders and materials are
/// on the roadmap.
/// </remarks>
public sealed class SilkRenderer : IRenderer
{
    private const int FloatsPerVertex = 8;
    private const int VerticesPerQuad = 4;
    private const int VertexStride = FloatsPerVertex * sizeof(float);
    private const int FontAtlasWidth = BitmapFontMetrics.GlyphCount * BitmapFontMetrics.GlyphWidth;

    private const string VertexShaderSource = """
        #version 330 core
        layout (location = 0) in vec2 aPosition;
        layout (location = 1) in vec2 aTexCoord;
        layout (location = 2) in vec4 aColor;
        uniform mat4 uProjection;
        out vec2 vTexCoord;
        out vec4 vColor;
        void main()
        {
            gl_Position = vec4(aPosition, 0.0, 1.0) * uProjection;
            vTexCoord = aTexCoord;
            vColor = aColor;
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        in vec2 vTexCoord;
        in vec4 vColor;
        uniform sampler2D uTexture;
        uniform int uUseTexture;
        out vec4 FragColor;
        void main()
        {
            if (uUseTexture == 1)
            {
                FragColor = texture(uTexture, vTexCoord) * vColor;
            }
            else
            {
                FragColor = vColor;
            }
        }
        """;

    private readonly float[] _vertexScratch = new float[FloatsPerVertex * VerticesPerQuad];
    private readonly float[] _matrixScratch = new float[16];
    private readonly Vector2[] _cornerScratch = new Vector2[VerticesPerQuad];

    private IWindowService? _windowService;
    private GL? _gl;
    private uint _program;
    private uint _vao;
    private uint _vbo;
    private uint _fontTexture;
    private int _projectionLocation;
    private int _textureLocation;
    private int _useTextureLocation;
    private Camera2D _camera = new() { Zoom = 1f };
    private bool _frameCleared;

    /// <inheritdoc />
    public Vector2 ViewportSize { get; private set; }

    /// <inheritdoc />
    public void Attach(IWindowService window)
    {
        ArgumentNullException.ThrowIfNull(window);

        // A second Attach would otherwise leave the objects of the first window behind.
        ReleaseResources();

        _windowService = window;
        _gl = GL.GetApi(window.Window);
        ViewportSize = new Vector2(window.Window.Size.X, window.Window.Size.Y);
        CreateResources();
    }

    /// <inheritdoc />
    public void Dispose() => ReleaseResources();

    /// <inheritdoc />
    public void SetCamera(Camera2D camera) => _camera = camera;

    /// <inheritdoc />
    public void BeginFrame(bool clear)
    {
        GL gl = RequireContext();
        RefreshViewport();

        if (clear && !_frameCleared)
        {
            gl.ClearColor(Color.Black.R / 255f, Color.Black.G / 255f, Color.Black.B / 255f, Color.Black.A / 255f);
            gl.Clear(ClearBufferMask.ColorBufferBit);
            _frameCleared = true;
        }

        gl.UseProgram(_program);
        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        float width = MathF.Max(ViewportSize.X, 1f);
        float height = MathF.Max(ViewportSize.Y, 1f);
        CopyTransposed(_camera.GetViewMatrix() * Matrix4x4.CreateOrthographic(width, height), _matrixScratch);

        unsafe
        {
            fixed (float* pointer = _matrixScratch)
            {
                gl.UniformMatrix4(_projectionLocation, 1, false, pointer);
            }
        }

        gl.Uniform1(_textureLocation, 0);
    }

    /// <inheritdoc />
    public void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color, float rotation = 0f) =>
        DrawTextureRegion(texture, new Rect(Vector2.Zero, new Vector2(1f, 1f)), position, size, color, rotation);

    /// <inheritdoc />
    public void DrawTextureRegion(TextureHandle texture, Rect source, Vector2 position, Vector2 size, Color color, float rotation = 0f) =>
        DrawQuad(position, size, color, texture.Id == 0 ? default : source, (uint)texture.Id, rotation);

    /// <inheritdoc />
    public TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        long required = (long)width * height * 4;
        if (required > int.MaxValue || pixels.Length != required)
        {
            throw new ArgumentException($"The buffer holds {pixels.Length} bytes, but {width} x {height} RGBA pixels need {required} bytes, which is not a supported texture size.", nameof(pixels));
        }

        GL gl = RequireContext();
        uint texture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, texture);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        unsafe
        {
            fixed (byte* pointer = pixels)
            {
                gl.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    InternalFormat.Rgba,
                    (uint)width,
                    (uint)height,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    pointer);
            }
        }

        gl.BindTexture(TextureTarget.Texture2D, 0);
        return new TextureHandle((int)texture);
    }

    /// <inheritdoc />
    public void ReleaseTexture(TextureHandle texture)
    {
        if (texture.Id == 0)
        {
            return;
        }

        RequireContext().DeleteTexture((uint)texture.Id);
    }

    /// <inheritdoc />
    public void DrawRectangle(Rect rect, Color color) => DrawQuad(rect.Position, rect.Size, color, default, 0u, 0f);

    /// <inheritdoc />
    public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color)
    {
        float cursor = position.X;

        foreach (char character in text)
        {
            // Only the characters that the font actually draws a mark for reach the draw call: the cell of the space in
            // the built-in atlas is blank, so drawing it would spend a quad on nothing. The cursor moves on for every
            // character either way, which is what keeps the words of a line apart.
            if (character > BitmapFontMetrics.FirstCharacter && character <= BitmapFontMetrics.LastCharacter)
            {
                int glyph = BitmapFontMetrics.GetGlyphIndex(character);
                float u0 = glyph * BitmapFontMetrics.GlyphWidth / (float)FontAtlasWidth;
                float u1 = ((glyph + 1) * BitmapFontMetrics.GlyphWidth) / (float)FontAtlasWidth;
                var uv = new Rect(new Vector2(u0, 0f), new Vector2(u1 - u0, 1f));

                DrawQuad(
                    new Vector2(cursor, position.Y),
                    new Vector2(BitmapFontMetrics.GlyphWidth, BitmapFontMetrics.GlyphHeight),
                    color,
                    uv,
                    _fontTexture,
                    0f);
            }

            cursor += BitmapFontMetrics.GlyphWidth;
        }
    }

    /// <inheritdoc />
    public void EndFrame()
    {
        GL gl = RequireContext();
        gl.BindTexture(TextureTarget.Texture2D, 0);
        gl.BindVertexArray(0);
        gl.UseProgram(0);
        _frameCleared = false;
    }

    private void CreateResources()
    {
        GL gl = RequireContext();
        _program = CreateProgram(gl);
        _projectionLocation = gl.GetUniformLocation(_program, "uProjection");
        _textureLocation = gl.GetUniformLocation(_program, "uTexture");
        _useTextureLocation = gl.GetUniformLocation(_program, "uUseTexture");

        _vao = gl.GenVertexArray();
        gl.BindVertexArray(_vao);
        _vbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        unsafe
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(VertexStride * VerticesPerQuad), (void*)0, BufferUsageARB.DynamicDraw);

            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, (uint)VertexStride, (void*)0);

            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, (uint)VertexStride, (void*)(2 * sizeof(float)));

            gl.EnableVertexAttribArray(2);
            gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, (uint)VertexStride, (void*)(4 * sizeof(float)));
        }

        _fontTexture = CreateFontTexture();
        gl.BindVertexArray(0);
    }

    private void RefreshViewport()
    {
        if (_windowService is null)
        {
            return;
        }

        var size = _windowService.Window.Size;
        ViewportSize = new Vector2(size.X, size.Y);
    }

    private readonly ILogger<SilkRenderer>? _logger;

    /// <summary>Initializes the renderer, which reports a frame that was drawn before the device was there.</summary>
    /// <param name="logger">The logger that reports a refused frame, or null to report nothing.</param>
    public SilkRenderer(ILogger<SilkRenderer>? logger = null) => _logger = logger;

    /// <summary>Returns the device of the attached window, and leaves a record when there is none, which is what a refused frame looks like.</summary>
    private GL RequireContext()
    {
        if (_gl is null)
        {
            _logger?.LogError("A frame was drawn before the renderer was attached to a window, so nothing was drawn.");
            throw new InvalidOperationException("The renderer has not been attached to a window.");
        }

        return _gl;
    }

    /// <summary>Deletes the program, the buffers and the font texture of this renderer. Safe to call when nothing was created.</summary>
    private void ReleaseResources()
    {
        if (_gl is null)
        {
            return;
        }

        GL gl = _gl;

        if (_fontTexture != 0)
        {
            gl.DeleteTexture(_fontTexture);
            _fontTexture = 0;
        }

        if (_vbo != 0)
        {
            gl.DeleteBuffer(_vbo);
            _vbo = 0;
        }

        if (_vao != 0)
        {
            gl.DeleteVertexArray(_vao);
            _vao = 0;
        }

        if (_program != 0)
        {
            gl.DeleteProgram(_program);
            _program = 0;
        }

        _gl = null;
        ViewportSize = Vector2.Zero;
    }

    private static uint CreateProgram(GL gl)
    {
        uint vertex = CompileShader(gl, ShaderType.VertexShader, VertexShaderSource);
        uint fragment = CompileShader(gl, ShaderType.FragmentShader, FragmentShaderSource);

        uint program = gl.CreateProgram();
        gl.AttachShader(program, vertex);
        gl.AttachShader(program, fragment);
        gl.LinkProgram(program);
        gl.DetachShader(program, vertex);
        gl.DetachShader(program, fragment);
        gl.DeleteShader(vertex);
        gl.DeleteShader(fragment);
        return program;
    }

    private static uint CompileShader(GL gl, ShaderType type, string source)
    {
        uint shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);

        if (status == 0)
        {
            string log = gl.GetShaderInfoLog(shader);
            gl.DeleteShader(shader);
            throw new InvalidOperationException($"Shader compilation failed: {log}");
        }

        return shader;
    }

    private void DrawQuad(Vector2 position, Vector2 size, Color color, Rect uv, uint texture, float rotation)
    {
        GL gl = RequireContext();

        SpriteQuad.Corners(position, size, rotation, _cornerScratch);

        float r = color.R / 255f;
        float g = color.G / 255f;
        float b = color.B / 255f;
        float a = color.A / 255f;

        WriteVertex(0, _cornerScratch[0].X, _cornerScratch[0].Y, uv.X, uv.Y, r, g, b, a);
        WriteVertex(1, _cornerScratch[1].X, _cornerScratch[1].Y, uv.X + uv.Width, uv.Y, r, g, b, a);
        WriteVertex(2, _cornerScratch[2].X, _cornerScratch[2].Y, uv.X, uv.Y + uv.Height, r, g, b, a);
        WriteVertex(3, _cornerScratch[3].X, _cornerScratch[3].Y, uv.X + uv.Width, uv.Y + uv.Height, r, g, b, a);

        unsafe
        {
            fixed (float* pointer = _vertexScratch)
            {
                gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(_vertexScratch.Length * sizeof(float)), pointer);
            }
        }

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, texture);
        gl.Uniform1(_useTextureLocation, texture == 0 ? 0 : 1);
        gl.DrawArrays(PrimitiveType.TriangleStrip, 0, VerticesPerQuad);
    }

    private void WriteVertex(int index, float x, float y, float u, float v, float r, float g, float b, float a)
    {
        int offset = index * FloatsPerVertex;
        _vertexScratch[offset] = x;
        _vertexScratch[offset + 1] = y;
        _vertexScratch[offset + 2] = u;
        _vertexScratch[offset + 3] = v;
        _vertexScratch[offset + 4] = r;
        _vertexScratch[offset + 5] = g;
        _vertexScratch[offset + 6] = b;
        _vertexScratch[offset + 7] = a;
    }

    private uint CreateFontTexture()
    {
        const int height = BitmapFontMetrics.GlyphHeight;
        byte[] pixels = new byte[FontAtlasWidth * height * 4];
        ReadOnlySpan<byte> glyphs = BuiltInFont.Data;

        for (int glyph = 0; glyph < BitmapFontMetrics.GlyphCount; glyph++)
        {
            for (int row = 0; row < height; row++)
            {
                byte bits = glyphs[(glyph * height) + row];

                for (int column = 0; column < BitmapFontMetrics.GlyphWidth; column++)
                {
                    if ((bits & (1 << column)) == 0)
                    {
                        continue;
                    }

                    int x = (glyph * BitmapFontMetrics.GlyphWidth) + column;
                    int index = ((row * FontAtlasWidth) + x) * 4;
                    pixels[index] = 255;
                    pixels[index + 1] = 255;
                    pixels[index + 2] = 255;
                    pixels[index + 3] = 255;
                }
            }
        }

        GL gl = RequireContext();
        uint texture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, texture);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        unsafe
        {
            fixed (byte* pointer = pixels)
            {
                gl.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    InternalFormat.Rgba,
                    (uint)FontAtlasWidth,
                    (uint)height,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    pointer);
            }
        }

        gl.BindTexture(TextureTarget.Texture2D, 0);
        return texture;
    }

    private static void CopyTransposed(Matrix4x4 matrix, float[] destination)
    {
        destination[0] = matrix.M11;
        destination[1] = matrix.M21;
        destination[2] = matrix.M31;
        destination[3] = matrix.M41;
        destination[4] = matrix.M12;
        destination[5] = matrix.M22;
        destination[6] = matrix.M32;
        destination[7] = matrix.M42;
        destination[8] = matrix.M13;
        destination[9] = matrix.M23;
        destination[10] = matrix.M33;
        destination[11] = matrix.M43;
        destination[12] = matrix.M14;
        destination[13] = matrix.M24;
        destination[14] = matrix.M34;
        destination[15] = matrix.M44;
    }
}
