namespace Age.Audio;

/// <summary>
/// How one playback of a sound is heard: its volume, its speed, where it stands between the two speakers, whether it
/// repeats, which group it is mixed into, and how important it is.
/// </summary>
/// <remarks>
/// A playback is a value: a game writes it where it plays a sound and never holds a mutable one. What a game changes
/// while a sound plays goes through the handle that the play returned, which is what keeps a setting a value and a
/// state a handle.
/// </remarks>
public struct AudioPlaybackSettings
{
    /// <summary>Gets or sets the volume of the playback, where one is the recorded level.</summary>
    public float Volume;

    /// <summary>Gets or sets the speed of the playback, where one is the recorded speed.</summary>
    /// <remarks>A speed above one plays the sound faster and higher, and one below plays it slower and lower, which is what a game uses for a random hit or a slowed-down effect.</remarks>
    public float Pitch;

    /// <summary>Gets or sets where the sound stands between the two speakers, where minus one is the left one and one is the right one.</summary>
    public float Pan;

    /// <summary>Gets or sets a value indicating whether the sound starts over when it reaches its end.</summary>
    public bool Loop;

    /// <summary>Gets or sets the group the sound is mixed into.</summary>
    public AudioChannel Channel;

    /// <summary>Gets or sets how important the playback is, which a device that ran out of voices uses to choose what to stop.</summary>
    /// <remarks>
    /// A playback of a higher priority takes a voice from one of a lower priority when every voice is busy, and a
    /// playback of a priority that no running one is below is dropped. A game gives a voice to the score and to the
    /// lines of a character and leaves the effects at the default, which is what keeps a hit from silencing a line.
    /// </remarks>
    public int Priority;

    /// <summary>Returns settings for a sound that plays once, at the recorded level, in the middle of the field, in a channel.</summary>
    /// <param name="channel">The group the sound is mixed into.</param>
    /// <returns>The settings, ready to be handed to a play.</returns>
    public static AudioPlaybackSettings Default(AudioChannel channel) => new()
    {
        Volume = 1f,
        Pitch = 1f,
        Pan = 0f,
        Loop = false,
        Channel = channel,
        Priority = 0,
    };
}