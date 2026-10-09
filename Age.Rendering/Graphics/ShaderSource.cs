namespace Age.Rendering;

/// <summary>
/// The stages that the engine writes itself: the header of every shader of a game, and the vertex stage that a fragment shader
/// of a game is drawn with.
/// </summary>
/// <remarks>
/// <para>
/// A shader of content is written in the OpenGL Shading Language, and the header is what gives it the names the engine binds:
/// <c>UV</c> is the texture coordinate of the fragment, <c>COLOR</c> is the colour it writes, <c>TEXTURE</c> is the sampler of
/// the image that is drawn, <c>TEXTURE_PIXEL_SIZE</c> is the size of one texel of it, <c>TIME</c> is the seconds since the
/// renderer was created, and <c>sampleTexture</c> reads the image. A shader therefore writes what it means rather than a walk
/// over the layout of a vertex, and the layout stays a decision of the renderer.
/// </para>
/// <para>
/// A game that writes a fragment shader only is the common case, and the standard vertex stage draws it: it places the quad of
/// the renderer and hands its texture coordinate and its colour on. A game that writes a vertex shader of its own takes the
/// placement over, and the header names what it reads (<c>VERTEX</c>, <c>UV</c>, <c>COLOR</c>) and what it writes
/// (<c>vTexCoord</c>, <c>vColor</c>).
/// </para>
/// <para>
/// The header is prepended to the source a game writes, so a line number that the compiler reports counts the lines of the
/// header as well: <see cref="VertexHeaderLines"/> and <see cref="FragmentHeaderLines"/> are what a renderer subtracts to name
/// the line of the file a game wrote.
/// </para>
/// </remarks>
internal static class ShaderSource
{
    /// <summary>The body of the vertex stage that a fragment shader of a game is drawn with.</summary>
    public const string StandardBody = """
        void main()
        {
            gl_Position = vec4(aPosition, 0.0, 1.0) * uProjection;
            vTexCoord = aTexCoord;
            vColor = aColor;
        }
        """;

    /// <summary>Gets the number of lines that the header of a vertex shader takes.</summary>
    public static int VertexHeaderLines { get; } = Lines(VertexHeader);

    /// <summary>Gets the number of lines that the header of a fragment shader takes.</summary>
    public static int FragmentHeaderLines { get; } = Lines(FragmentHeader);

    private const string VertexHeader = """
        #version 330 core
        layout (location = 0) in vec2 aPosition;
        layout (location = 1) in vec2 aTexCoord;
        layout (location = 2) in vec4 aColor;
        uniform mat4 uProjection;
        uniform float uTime;
        out vec2 vTexCoord;
        out vec4 vColor;
        #define VERTEX aPosition
        #define UV aTexCoord
        #define COLOR aColor
        #define TIME uTime

        """;

    private const string FragmentHeader = """
        #version 330 core
        in vec2 vTexCoord;
        in vec4 vColor;
        uniform sampler2D uTexture;
        uniform float uTime;
        out vec4 FragColor;
        #define UV vTexCoord
        #define COLOR FragColor
        #define TEXTURE uTexture
        #define TEXTURE_PIXEL_SIZE (1.0 / vec2(textureSize(uTexture, 0)))
        #define TIME uTime

        vec4 sampleTexture(vec2 uv)
        {
            return texture(uTexture, uv);
        }

        """;

    /// <summary>Returns the vertex stage of a shader: the header and the stage a game wrote, or the standard one.</summary>
    /// <param name="source">The stage a game wrote, or null to draw with the standard vertex stage.</param>
    /// <returns>The stage that the device compiles.</returns>
    public static string Vertex(string? source) => VertexHeader + (source ?? StandardBody);

    /// <summary>Returns the fragment stage of a shader: the header and the stage a game wrote.</summary>
    /// <param name="source">The stage a game wrote.</param>
    /// <returns>The stage that the device compiles.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static string Fragment(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return FragmentHeader + source;
    }

    /// <summary>Returns the number of lines of a stage's header, which is what a compile error counts from.</summary>
    /// <param name="header">The header to count.</param>
    /// <returns>The number of lines, counting the line that follows the last line feed of the header.</returns>
    private static int Lines(string header)
    {
        var lines = 0;

        foreach (char character in header)
        {
            if (character == '\n')
            {
                lines++;
            }
        }

        return lines;
    }
}
