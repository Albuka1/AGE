namespace Age.Core;

/// <summary>
/// Identifies a resource inside a <see cref="ResourcePool{T}"/>. The handle records the pool that created it, the slot
/// and the slot generation, so a handle that was forged, taken from another pool, or left behind by a resource that was
/// released and replaced no longer matches anything.
/// </summary>
public readonly struct ResourceHandle : IEquatable<ResourceHandle>
{
    private static int _nextOwner;

    /// <summary>Initializes a handle. Handles normally come from a pool; a forged handle is rejected on lookup.</summary>
    public ResourceHandle(int owner, int id, int generation)
    {
        Owner = owner;
        Id = id;
        Generation = generation;
    }

    /// <summary>Gets the token of the pool that created the handle. Zero means that the handle refers to nothing.</summary>
    public int Owner { get; }

    /// <summary>Gets the identifier of the slot. Zero means that the handle refers to nothing.</summary>
    public int Id { get; }

    /// <summary>Gets the generation of the slot.</summary>
    public int Generation { get; }

    /// <summary>Gets a value indicating whether the handle refers to a slot. A default handle is invalid.</summary>
    public bool IsValid => Id > 0 && Owner > 0;

    /// <summary>Returns the next pool token. Tokens are unique for the life of the process.</summary>
    internal static int NextOwner() => Interlocked.Increment(ref _nextOwner);

    /// <summary>Determines whether two handles are equal.</summary>
    public static bool operator ==(ResourceHandle left, ResourceHandle right) => left.Equals(right);

    /// <summary>Determines whether two handles are not equal.</summary>
    public static bool operator !=(ResourceHandle left, ResourceHandle right) => !left.Equals(right);

    /// <summary>Determines whether this handle equals another handle.</summary>
    public bool Equals(ResourceHandle other) => Owner == other.Owner && Id == other.Id && Generation == other.Generation;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ResourceHandle other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Owner, Id, Generation);
}
