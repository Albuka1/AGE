using Age.Core;
using Silk.NET.OpenGL;

namespace Age.Rendering;

/// <summary>
/// An OpenGL 3.3 core renderer. It uses a private shader shared by DrawRectangle, DrawSprite and DrawText.
/// User-facing shaders and materials are planned; fixed-function drawing is not used.
/// </summary>
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

        _windowService = window;
        _gl = GL.GetApi(window.Window);
        ViewportSize = new Vector2(window.Window.Size.X, window.Window.Size.Y);
        CreateResources();
    }

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
    public void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color)
    {
        if (texture.Id == 0)
        {
            DrawQuad(position, size, color, default, 0u);
            return;
        }

        DrawQuad(position, size, color, new Rect(Vector2.Zero, new Vector2(1f, 1f)), (uint)texture.Id);
    }

    /// <inheritdoc />
    public void DrawRectangle(Rect rect, Color color) => DrawQuad(rect.Position, rect.Size, color, default, 0u);

    /// <inheritdoc />
    public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color)
    {
        float cursor = position.X;

        foreach (char character in text)
        {
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
                    _fontTexture);
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

    private GL RequireContext() => _gl ?? throw new InvalidOperationException("The renderer has not been attached to a window.");

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

    private void DrawQuad(Vector2 position, Vector2 size, Color color, Rect uv, uint texture)
    {
        GL gl = RequireContext();

        float x0 = position.X;
        float y0 = position.Y;
        float x1 = position.X + size.X;
        float y1 = position.Y + size.Y;
        float r = color.R / 255f;
        float g = color.G / 255f;
        float b = color.B / 255f;
        float a = color.A / 255f;

        WriteVertex(0, x0, y0, uv.X, uv.Y, r, g, b, a);
        WriteVertex(1, x1, y0, uv.X + uv.Width, uv.Y, r, g, b, a);
        WriteVertex(2, x0, y1, uv.X, uv.Y + uv.Height, r, g, b, a);
        WriteVertex(3, x1, y1, uv.X + uv.Width, uv.Y + uv.Height, r, g, b, a);

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
