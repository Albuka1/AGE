using Age.Core;

namespace Age.Audio;

/// <summary>
/// Identifies a sound that an <see cref="ISoundService"/> loaded. A value of zero means that the device holds no sound
/// for the handle, which is what a device that discards sounds reports.
/// </summary>
public readonly struct SoundHandle : IEquatable<SoundHandle>
{
    /// <summary>Gets the identifier of the sound inside the audio device. Zero means that no sound is bound.</summary>
    public int Id { get; }

    /// <summary>Gets the slot the sound occupies in the service that loaded it. A handle made by the device alone has no slot.</summary>
    internal ResourceHandle Resource { get; }

    /// <summary>Initializes a handle from a device identifier.</summary>
    /// <param name="id">The identifier of the sound inside the audio device.</param>
    public SoundHandle(int id) => Id = id;

    /// <summary>Initializes a handle together with the slot it occupies in the service that owns the sound.</summary>
    internal SoundHandle(ResourceHandle resource, int id)
    {
        Resource = resource;
        Id = id;
    }

    /// <summary>Determines whether two handles are equal.</summary>
    public static bool operator ==(SoundHandle left, SoundHandle right) => left.Equals(right);

    /// <summary>Determines whether two handles are not equal.</summary>
    public static bool operator !=(SoundHandle left, SoundHandle right) => !left.Equals(right);

    /// <summary>Determines whether this handle equals another handle.</summary>
    public bool Equals(SoundHandle other) => Id == other.Id && Resource == other.Resource;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SoundHandle other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Id, Resource);
}
