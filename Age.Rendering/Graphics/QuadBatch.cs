using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Collects the quads of one program and one texture, so that a frame draws them in one call instead of one call each.
/// </summary>
/// <remarks>
/// <para>
/// A vertex of the renderer is a position, a texture coordinate and a colour, and a quad is four of them: its top-left, its
/// top-right, its bottom-left and its bottom-right. The batch answers whether the next quad fits it, which is what a renderer
/// asks before it draws: a quad of another program or of another texture ends a batch, because a draw call samples one texture
/// with one program, and so does a quad beyond the capacity of the batch.
/// </para>
/// <para>
/// The indices of the two triangles of a quad are the same for every quad, so they are built once for the whole capacity and
/// shifted by the number of the quad, which is what lets one call draw every quad that the batch holds. A batch is pure
/// bookkeeping: it holds no device object and it draws nothing, so what it holds is something a test pins down without a window.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// if (!batch.CanAdd(program, texture))
/// {
///     Flush(batch);
/// }
///
/// batch.Add(corners, uv, color, program, texture);
/// </code>
/// </example>
internal sealed class QuadBatch
{
    /// <summary>The number of floats that one vertex holds: a position, a texture coordinate and a colour.</summary>
    public const int FloatsPerVertex = 8;

    /// <summary>The number of vertices of one quad.</summary>
    public const int VerticesPerQuad = 4;

    /// <summary>The number of indices of one quad, which are its two triangles.</summary>
    public const int IndicesPerQuad = 6;

    private readonly float[] _vertices;
    private readonly uint[] _indices;
    private int _quads;

    /// <summary>Initializes a batch that holds a number of quads.</summary>
    /// <param name="quads">The number of quads the batch holds before it is full.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quads"/> is zero or negative.</exception>
    public QuadBatch(int quads)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quads);
        _vertices = new float[quads * VerticesPerQuad * FloatsPerVertex];
        _indices = new uint[quads * IndicesPerQuad];

        for (var quad = 0; quad < quads; quad++)
        {
            int vertex = quad * VerticesPerQuad;
            int index = quad * IndicesPerQuad;

            // The first triangle is the top-left, the top-right and the bottom-left of the quad, and the second is the
            // top-right, the bottom-right and the bottom-left of it.
            _indices[index] = (uint)vertex;
            _indices[index + 1] = (uint)(vertex + 1);
            _indices[index + 2] = (uint)(vertex + 2);
            _indices[index + 3] = (uint)(vertex + 1);
            _indices[index + 4] = (uint)(vertex + 3);
            _indices[index + 5] = (uint)(vertex + 2);
        }
    }

    /// <summary>Gets the number of quads that the batch holds.</summary>
    public int Count => _quads;

    /// <summary>Gets the number of quads that the batch holds when it is full.</summary>
    public int Capacity => _vertices.Length / (VerticesPerQuad * FloatsPerVertex);

    /// <summary>Gets the identifier of the program that the batch draws with, or zero while it holds nothing.</summary>
    public uint Program { get; private set; }

    /// <summary>Gets the identifier of the texture that the batch samples, or zero while it holds nothing.</summary>
    public uint Texture { get; private set; }

    /// <summary>Gets the vertices of the quads that the batch holds, which is four vertices per quad.</summary>
    public ReadOnlySpan<float> Vertices => _vertices.AsSpan(0, _quads * VerticesPerQuad * FloatsPerVertex);

    /// <summary>Gets the indices of the triangles of the quads that the batch holds.</summary>
    public ReadOnlySpan<uint> Indices => _indices.AsSpan(0, _quads * IndicesPerQuad);

    /// <summary>Returns a value indicating whether a quad of a program and a texture fits the batch.</summary>
    /// <param name="program">The identifier of the program the quad is drawn with.</param>
    /// <param name="texture">The identifier of the texture the quad samples.</param>
    /// <returns><see langword="true"/> while the batch holds nothing, or holds quads of that program and that texture and has room for another one.</returns>
    public bool CanAdd(uint program, uint texture) =>
        _quads == 0 ? program != 0 : _quads < Capacity && Program == program && Texture == texture;

    /// <summary>Gets the indices of every quad that the batch holds when it is full, which is what a renderer uploads once.</summary>
    /// <remarks>The indices of the quads that are held now are the beginning of this span, so one call draws them all.</remarks>
    public ReadOnlySpan<uint> AllIndices => _indices;

    /// <summary>Adds the four corners of a quad, the part of the texture that it shows and its colour.</summary>
    /// <param name="corners">The corners of the quad: its top-left, its top-right, its bottom-left and its bottom-right.</param>
    /// <param name="uv">The part of the texture that the quad shows, in normalized coordinates.</param>
    /// <param name="color">The tint of the quad.</param>
    /// <param name="program">The identifier of the program the quad is drawn with, which the first quad of a batch decides.</param>
    /// <param name="texture">The identifier of the texture the quad samples, which the first quad of a batch decides.</param>
    /// <exception cref="ArgumentException"><paramref name="corners"/> does not hold four corners.</exception>
    /// <exception cref="InvalidOperationException">The batch is full, or it holds quads of another program or of another texture.</exception>
    /// <remarks>The texture coordinate of the top-left corner of the quad is the position of the part that is shown, and the other corners add its width and its height, which is how a part of an atlas reaches a quad the way a whole image does.</remarks>
    public void Add(ReadOnlySpan<Vector2> corners, Rect uv, Color color, uint program, uint texture)
    {
        if (corners.Length != VerticesPerQuad)
        {
            throw new ArgumentException($"A quad has four corners, and {corners.Length} were given.", nameof(corners));
        }

        if (!CanAdd(program, texture))
        {
            throw new InvalidOperationException("The batch is full, or it holds quads of another program or of another texture, which is what a caller flushes it for.");
        }

        if (_quads == 0)
        {
            Program = program;
            Texture = texture;
        }

        float r = color.R / 255f;
        float g = color.G / 255f;
        float b = color.B / 255f;
        float a = color.A / 255f;
        int offset = _quads * VerticesPerQuad * FloatsPerVertex;

        Write(offset, corners[0], uv.X, uv.Y, r, g, b, a);
        Write(offset + FloatsPerVertex, corners[1], uv.X + uv.Width, uv.Y, r, g, b, a);
        Write(offset + (2 * FloatsPerVertex), corners[2], uv.X, uv.Y + uv.Height, r, g, b, a);
        Write(offset + (3 * FloatsPerVertex), corners[3], uv.X + uv.Width, uv.Y + uv.Height, r, g, b, a);

        _quads++;
    }

    /// <summary>Empties the batch, which is what a renderer does once it drew what the batch held.</summary>
    public void Clear()
    {
        _quads = 0;
        Program = 0;
        Texture = 0;
    }

    /// <summary>Writes one vertex of the batch.</summary>
    /// <param name="offset">The position of the vertex in the array of the batch.</param>
    /// <param name="position">The position of the vertex.</param>
    /// <param name="u">The horizontal texture coordinate of the vertex.</param>
    /// <param name="v">The vertical texture coordinate of the vertex.</param>
    /// <param name="r">The red channel of the colour.</param>
    /// <param name="g">The green channel of the colour.</param>
    /// <param name="b">The blue channel of the colour.</param>
    /// <param name="a">The alpha channel of the colour.</param>
    private void Write(int offset, Vector2 position, float u, float v, float r, float g, float b, float a)
    {
        _vertices[offset] = position.X;
        _vertices[offset + 1] = position.Y;
        _vertices[offset + 2] = u;
        _vertices[offset + 3] = v;
        _vertices[offset + 4] = r;
        _vertices[offset + 5] = g;
        _vertices[offset + 6] = b;
        _vertices[offset + 7] = a;
    }
}
