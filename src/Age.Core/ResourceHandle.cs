namespace Age.Core;

/// <summary>
/// Identifies a resource inside a <see cref="ResourcePool{T}"/>. The handle carries the slot generation, so a handle
/// that points at a resource which was released and replaced no longer matches anything.
/// </summary>
public readonly struct ResourceHandle : IEquatable<ResourceHandle>
{
    /// <summary>Initializes a handle. Handles normally come from a pool; a forged handle is rejected on lookup.</summary>
    public ResourceHandle(int id, int generation)
    {
        Id = id;
        Generation = generation;
    }

    /// <summary>Gets the identifier of the slot. Zero means that the handle refers to nothing.</summary>
    public int Id { get; }

    /// <summary>Gets the generation of the slot.</summary>
    public int Generation { get; }

    /// <summary>Gets a value indicating whether the handle refers to a slot. A default handle is invalid.</summary>
    public bool IsValid => Id > 0;

    /// <summary>Determines whether two handles are equal.</summary>
    public static bool operator ==(ResourceHandle left, ResourceHandle right) => left.Equals(right);

    /// <summary>Determines whether two handles are not equal.</summary>
    public static bool operator !=(ResourceHandle left, ResourceHandle right) => !left.Equals(right);

    /// <summary>Determines whether this handle equals another handle.</summary>
    public bool Equals(ResourceHandle other) => Id == other.Id && Generation == other.Generation;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ResourceHandle other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Id, Generation);
}
