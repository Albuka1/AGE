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
    private const int FloatsPerVertex = QuadBatch.FloatsPerVertex;
    private const int VerticesPerQuad = QuadBatch.VerticesPerQuad;
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

    /// <summary>The number of quads that one batch of this renderer holds before it is drawn and starts over.</summary>
    private const int MaximumQuads = 4096;

    /// <summary>The part of a texture that a quad with a colour of its own shows, which is the whole of the pixel that it samples.</summary>
    private static readonly Rect UnitSquare = new(Vector2.Zero, new Vector2(1f, 1f));

    private readonly float[] _matrixScratch = new float[16];
    private readonly Vector2[] _cornerScratch = new Vector2[VerticesPerQuad];
    private readonly QuadBatch _batch = new(MaximumQuads);
    private uint _indexBuffer;
    private uint _whiteTexture;

    private IWindowService? _windowService;
    private GL? _gl;
    private uint _program;
    private uint _currentProgram;
    private uint _vao;
    private uint _vbo;
    private uint _fontTexture;
    private readonly Dictionary<string, int> _uniforms = new(StringComparer.Ordinal);
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
        // What a caller drew before this frame is drawn now, with the state it was collected under, which is the projection of
        // the camera that was set when it drew: a pass that sets the camera and then opens its frame keeps the order of the
        // frames it makes.
        Flush();

        GL gl = RequireContext();
        RefreshViewport();

        if (clear && !_frameCleared)
        {
            gl.ClearColor(Color.Black.R / 255f, Color.Black.G / 255f, Color.Black.B / 255f, Color.Black.A / 255f);
            gl.Clear(ClearBufferMask.ColorBufferBit);
            _frameCleared = true;
        }

        gl.UseProgram(_currentProgram);
        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        ApplyStandardUniforms(gl);
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
        Flush();

        GL gl = RequireContext();
        gl.BindTexture(TextureTarget.Texture2D, 0);
        gl.BindVertexArray(0);
        gl.UseProgram(0);
        _frameCleared = false;
    }

    private void CreateResources()
    {
        GL gl = RequireContext();
        _program = CreateProgram(gl, VertexShaderSource, FragmentShaderSource);
        _currentProgram = _program;

        _vao = gl.GenVertexArray();
        gl.BindVertexArray(_vao);
        _vbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        unsafe
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(VertexStride * VerticesPerQuad * MaximumQuads), (void*)0, BufferUsageARB.DynamicDraw);

            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, (uint)VertexStride, (void*)0);

            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, (uint)VertexStride, (void*)(2 * sizeof(float)));

            gl.EnableVertexAttribArray(2);
            gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, (uint)VertexStride, (void*)(4 * sizeof(float)));
        }

        // The indices of the quads never change, so they are uploaded once and bound while the vertex array is bound: a draw of
        // the batch is the beginning of them, which is what lets one call draw every quad that the batch holds.
        _indexBuffer = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _indexBuffer);

        ReadOnlySpan<uint> indices = _batch.AllIndices;

        unsafe
        {
            fixed (uint* pointer = indices)
            {
                gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), pointer, BufferUsageARB.StaticDraw);
            }
        }

        _whiteTexture = CreateWhiteTexture();
        _fontTexture = CreateFontTexture();
        gl.BindVertexArray(0);
    }

    /// <summary>Creates the texture of one white pixel that a quad with a colour of its own samples, so that every quad is a textured one.</summary>
    /// <returns>The identifier of the texture, which this renderer owns and releases.</returns>
    private uint CreateWhiteTexture()
    {
        GL gl = RequireContext();
        uint texture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, texture);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        ReadOnlySpan<byte> pixel = [255, 255, 255, 255];

        unsafe
        {
            fixed (byte* pointer = pixel)
            {
                gl.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    InternalFormat.Rgba,
                    1,
                    1,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    pointer);
            }
        }

        gl.BindTexture(TextureTarget.Texture2D, 0);
        return texture;
    }

    /// <inheritdoc />
    public uint CompileShader(string vertexSource, string fragmentSource)
    {
        ArgumentNullException.ThrowIfNull(vertexSource);
        ArgumentNullException.ThrowIfNull(fragmentSource);

        return CreateProgram(RequireContext(), vertexSource, fragmentSource);
    }

    /// <inheritdoc />
    public void ReleaseShader(uint program)
    {
        if (program == 0)
        {
            return;
        }

        if (_currentProgram == program)
        {
            ResetShader();
        }

        RequireContext().DeleteProgram(program);
    }

    /// <inheritdoc />
    public void UseShader(ShaderHandle shader)
    {
        if (shader.Program == 0)
        {
            throw new ArgumentException("The handle is not a shader that the engine compiled.", nameof(shader));
        }

        // A draw call samples one program, so what a caller collected under the shader that is being replaced is drawn first.
        Flush();

        GL gl = RequireContext();
        _currentProgram = shader.Program;
        _uniforms.Clear();
        gl.UseProgram(_currentProgram);
        ApplyStandardUniforms(gl);
    }

    /// <inheritdoc />
    public void ResetShader()
    {
        Flush();

        GL gl = RequireContext();
        _currentProgram = _program;
        _uniforms.Clear();
        gl.UseProgram(_currentProgram);
        ApplyStandardUniforms(gl);
    }

    /// <inheritdoc />
    public void SetUniform(string name, float value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Flush();

        GL gl = RequireContext();
        gl.Uniform1(Location(gl, name), value);
    }

    /// <inheritdoc />
    public void SetUniform(string name, int value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Flush();

        GL gl = RequireContext();
        gl.Uniform1(Location(gl, name), value);
    }

    /// <inheritdoc />
    public void SetUniform(string name, ReadOnlySpan<float> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (values.Length is < 1 or > 4)
        {
            throw new ArgumentException($"A uniform of {values.Length} numbers is not one that a shader of one, two, three or four numbers takes.", nameof(values));
        }

        Flush();

        GL gl = RequireContext();
        int location = Location(gl, name);

        switch (values.Length)
        {
            case 1:
                gl.Uniform1(location, values[0]);
                break;
            case 2:
                gl.Uniform2(location, values[0], values[1]);
                break;
            case 3:
                gl.Uniform3(location, values[0], values[1], values[2]);
                break;
            default:
                gl.Uniform4(location, values[0], values[1], values[2], values[3]);
                break;
        }
    }

    /// <inheritdoc />
    public void SetSampler(string name, TextureHandle texture, int unit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(unit);

        Flush();

        GL gl = RequireContext();
        gl.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + unit));
        gl.BindTexture(TextureTarget.Texture2D, (uint)texture.Id);
        gl.Uniform1(Location(gl, name), unit);
        gl.ActiveTexture(TextureUnit.Texture0);
    }

    /// <summary>Returns the location of a uniform of the program that is being used, which is looked up once for that program.</summary>
    /// <param name="gl">The device of the attached window.</param>
    /// <param name="name">The name of the uniform, as a shader declares it.</param>
    /// <returns>The location of the uniform, or -1 when the program declares no uniform of that name, which the device ignores.</returns>
    private int Location(GL gl, string name)
    {
        if (_uniforms.TryGetValue(name, out int location))
        {
            return location;
        }

        location = gl.GetUniformLocation(_currentProgram, name);
        _uniforms[name] = location;

        return location;
    }

    /// <summary>Uploads what every shader of the engine is given: the projection of the camera, the time and the sampler of the image.</summary>
    /// <param name="gl">The device of the attached window.</param>
    /// <remarks>
    /// A program that declares none of these ignores what it is told about, so the engine uploads them to every program rather
    /// than asking which one is being used.
    /// </remarks>
    private void ApplyStandardUniforms(GL gl)
    {
        float width = MathF.Max(ViewportSize.X, 1f);
        float height = MathF.Max(ViewportSize.Y, 1f);
        CopyTransposed(_camera.GetViewMatrix() * Matrix4x4.CreateOrthographic(width, height), _matrixScratch);

        unsafe
        {
            fixed (float* pointer = _matrixScratch)
            {
                gl.UniformMatrix4(Location(gl, "uProjection"), 1, false, pointer);
            }
        }

        // Every quad samples a texture, the white pixel of one for a quad that carries a colour of its own, so the shader of the
        // engine takes its textured path and the sampler of the image is the first unit. The time is what a shader animates on,
        // and it is the time of the machine rather than the time of a simulation: a game that wants the time of its own sets
        // the uniform itself.
        gl.Uniform1(Location(gl, "uTexture"), 0);
        gl.Uniform1(Location(gl, "uUseTexture"), 1);
        gl.Uniform1(Location(gl, "uTime"), Environment.TickCount64 / 1000f);
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

        if (_whiteTexture != 0)
        {
            gl.DeleteTexture(_whiteTexture);
            _whiteTexture = 0;
        }

        if (_indexBuffer != 0)
        {
            gl.DeleteBuffer(_indexBuffer);
            _indexBuffer = 0;
        }

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

    private static uint CreateProgram(GL gl, string vertexSource, string fragmentSource)
    {
        uint vertex = CompileShader(gl, ShaderType.VertexShader, vertexSource);
        uint fragment = CompileShader(gl, ShaderType.FragmentShader, fragmentSource);

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
        // A quad without a texture is drawn as its colour alone, and the white pixel of the texture of one pixel is what makes
        // that a textured draw like any other: every quad then samples a texture, so a batch holds the quads of an image and the
        // quads of a solid colour in one call.
        uint sampler = texture == 0 ? _whiteTexture : texture;
        Rect source = texture == 0 ? UnitSquare : uv;

        if (!_batch.CanAdd(_currentProgram, sampler))
        {
            Flush();
        }

        SpriteQuad.Corners(position, size, rotation, _cornerScratch);
        _batch.Add(_cornerScratch, source, color, _currentProgram, sampler);
    }

    /// <summary>Draws every quad that the batch holds, which is what ends a batch: another texture, another program, or the end of a frame.</summary>
    /// <remarks>
    /// The vertices of the batch are uploaded and drawn in one call, and the texture of the batch is bound once, which is what
    /// makes a frame of a game cost a handful of calls rather than one for every glyph of a line, every sprite of a map and
    /// every rectangle of an interface. The indices of the quads live in a buffer of their own, which was filled once when the
    /// device was created, so a draw of fewer quads than the batch holds is the beginning of it.
    /// </remarks>
    private void Flush()
    {
        if (_batch.Count == 0)
        {
            return;
        }

        GL gl = RequireContext();

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _batch.Texture);

        ReadOnlySpan<float> vertices = _batch.Vertices;

        unsafe
        {
            fixed (float* pointer = vertices)
            {
                gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(vertices.Length * sizeof(float)), pointer);
            }
        }

        unsafe
        {
            gl.DrawElements((GLEnum)PrimitiveType.Triangles, (uint)_batch.Indices.Length, (GLEnum)DrawElementsType.UnsignedInt, (void*)0);
        }

        _batch.Clear();
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
