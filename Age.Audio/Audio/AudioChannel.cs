namespace Age.Audio;

/// <summary>
/// One of the groups a game mixes its sounds into: a volume of its own, applied on top of the master and of the
/// volumes of the playbacks under it.
/// </summary>
/// <remarks>
/// <para>
/// A game writes the volume of a channel and not of every sound: the score can be turned down while the effects stay
/// at their level, and the master is what a settings menu writes. The volume of a playback is its own volume,
/// multiplied by the volume of its channel, multiplied by the volume of the master.
/// </para>
/// <para>
/// The set is fixed rather than registered, because a game of two dimensions needs the same handful of groups, and
/// naming them here is what lets a settings menu be the same handful of rows in every game.
/// </para>
/// </remarks>
public enum AudioChannel
{
    /// <summary>The channel every other one is under. Its volume multiplies the whole mix.</summary>
    Master,

    /// <summary>The score, which a game plays one or a few of, usually looping.</summary>
    Music,

    /// <summary>The sounds of the world: hits, footsteps, explosions.</summary>
    Effects,

    /// <summary>What the characters say.</summary>
    Voice,

    /// <summary>What a menu is heard through: a click, a hover, a refusal.</summary>
    Ui,

    /// <summary>The ambience of a place, such as wind or rain, which plays under the score.</summary>
    Ambient,
}