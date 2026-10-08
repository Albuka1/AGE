namespace Age.Core;

/// <summary>
/// Identifies a resource inside a specific <see cref="ResourcePool{TKey, T}"/>.
/// </summary>
/// <remarks>
/// A handle is a small value, so it can be copied and stored in components freely. It is only meaningful for the pool
/// that created it: it records that pool, its slot and the slot generation, so a default value, a handle taken from
/// another pool and a handle left behind when its resource was released and replaced all fail to resolve. Only
/// <see cref="ResourcePool{TKey, T}.Add(T, TKey)"/> creates handles, because the constructor is not public.
/// </remarks>
/// <example>
/// <code>
/// ResourceHandle handle = pool.Add(deviceId, "art/player.png");
///
/// if (handle.IsValid &amp;&amp; pool.TryGet(handle, out uint id))
/// {
///     // id is the live device texture
/// }
/// </code>
/// </example>
public readonly struct ResourceHandle : IEquatable<ResourceHandle>
{
    private static int _nextOwner;

    /// <summary>Initializes a handle. Only a <see cref="ResourcePool{TKey, T}"/> creates handles, so a caller cannot forge one.</summary>
    internal ResourceHandle(int owner, int id, int generation)
    {
        Owner = owner;
        Id = id;
        Generation = generation;
    }

    /// <summary>Gets the token of the pool that created the handle. Zero means that the handle refers to nothing.</summary>
    public int Owner { get; }

    /// <summary>Gets the identifier of the slot. Zero means that the handle refers to nothing.</summary>
    public int Id { get; }

    /// <summary>Gets the generation of the slot. It changes every time the slot is handed out, which is what makes an older handle for the same slot stale.</summary>
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
