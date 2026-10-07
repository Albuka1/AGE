using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Identifies a texture owned by the graphics device. A value of zero selects a solid color quad.
/// </summary>
public readonly struct TextureHandle : IEquatable<TextureHandle>
{
    /// <summary>Gets the underlying device texture identifier. Zero means that no texture is bound.</summary>
    public int Id { get; }

    /// <summary>Gets the slot the texture occupies in the service that loaded it. A handle made by the renderer has no slot.</summary>
    internal ResourceHandle Resource { get; }

    /// <summary>Initializes a handle from a device texture identifier.</summary>
    public TextureHandle(int id) => Id = id;

    /// <summary>Initializes a handle together with the slot it occupies in the service that owns the texture.</summary>
    internal TextureHandle(ResourceHandle resource, int id)
    {
        Resource = resource;
        Id = id;
    }

    /// <summary>Determines whether two handles are equal.</summary>
    public static bool operator ==(TextureHandle left, TextureHandle right) => left.Equals(right);

    /// <summary>Determines whether two handles are not equal.</summary>
    public static bool operator !=(TextureHandle left, TextureHandle right) => !left.Equals(right);

    /// <summary>Determines whether this handle equals another handle.</summary>
    public bool Equals(TextureHandle other) => Id == other.Id && Resource == other.Resource;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TextureHandle other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Id, Resource);
}
