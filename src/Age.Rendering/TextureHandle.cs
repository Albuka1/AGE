namespace Age.Rendering;

/// <summary>
/// Identifies a texture owned by the graphics device. A value of zero selects a solid color quad.
/// </summary>
public readonly struct TextureHandle
{
    /// <summary>Gets the underlying device texture identifier. Zero means that no texture is bound.</summary>
    public int Id { get; }

    /// <summary>Initializes a handle from a device texture identifier.</summary>
    public TextureHandle(int id) => Id = id;
}
