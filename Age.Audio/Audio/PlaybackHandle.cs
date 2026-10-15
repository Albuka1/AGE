using Age.Core;

namespace Age.Audio;

/// <summary>
/// Identifies one playback of a sound: the voice inside the pool of the device, and the generation of the slot it
/// occupies, so a handle from before the voice was reused stops resolving.
/// </summary>
/// <remarks>
/// A playback ends on its own when a sound that does not loop reaches its end, and a handle that a game keeps is not a
/// promise that the sound is still heard: what a game asks of a handle is whether the device still holds the voice,
/// and what it does with one that is gone is up to it. A default value names nothing.
/// </remarks>
public readonly struct PlaybackHandle : IEquatable<PlaybackHandle>
{
    /// <summary>Gets the handle that names no playback.</summary>
    public static PlaybackHandle None => default;

    /// <summary>Initializes a handle. Only the engine creates handles.</summary>
    /// <param name="voice">The slot of the voice inside the pool of the device.</param>
    internal PlaybackHandle(ResourceHandle voice) => Voice = voice;

    /// <summary>Gets the slot of the voice inside the pool of the device, or an invalid handle when the playback is gone.</summary>
    internal ResourceHandle Voice { get; }

    /// <summary>Gets a value indicating whether the handle names a voice at all.</summary>
    public bool IsValid => Voice.IsValid;

    /// <summary>Determines whether two handles name the same playback.</summary>
    public bool Equals(PlaybackHandle other) => Voice == other.Voice;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PlaybackHandle other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Voice.GetHashCode();

    /// <summary>Determines whether two handles name the same playback.</summary>
    public static bool operator ==(PlaybackHandle left, PlaybackHandle right) => left.Equals(right);

    /// <summary>Determines whether two handles name different playbacks.</summary>
    public static bool operator !=(PlaybackHandle left, PlaybackHandle right) => !left.Equals(right);
}