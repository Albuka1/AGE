using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Identifies a font that an <see cref="IFontService"/> baked into a glyph atlas.
/// </summary>
/// <remarks>
/// A handle records the slot of the service that baked the font, so a default value and a handle left behind when its
/// font was unloaded both stop resolving instead of naming the atlas that took the slot. Only
/// <see cref="IFontService.Load"/> creates handles, because the constructor is not public.
/// </remarks>
public readonly struct FontHandle : IEquatable<FontHandle>
{
    /// <summary>Initializes a handle. Only an <see cref="IFontService"/> creates handles.</summary>
    internal FontHandle(ResourceHandle resource, int atlas)
    {
        Resource = resource;
        Atlas = atlas;
    }

    /// <summary>Gets the identifier of the device texture that holds the glyph atlas. Zero means that no atlas is selected.</summary>
    public int Atlas { get; }

    /// <summary>Gets the slot that the font occupies in the service that baked it.</summary>
    internal ResourceHandle Resource { get; }

    /// <summary>Determines whether two handles refer to the same font.</summary>
    public static bool operator ==(FontHandle left, FontHandle right) => left.Equals(right);

    /// <summary>Determines whether two handles refer to different fonts.</summary>
    public static bool operator !=(FontHandle left, FontHandle right) => !left.Equals(right);

    /// <summary>Determines whether this handle equals another handle.</summary>
    public bool Equals(FontHandle other) => Atlas == other.Atlas && Resource == other.Resource;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is FontHandle other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Atlas, Resource);
}
