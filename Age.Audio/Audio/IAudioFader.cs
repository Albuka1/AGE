using Age.Core;

namespace Age.Audio;

/// <summary>
/// Fades the volume of a channel or of a playback over a duration, one frame at a time, which is what a game uses for a
/// menu that slides in and for a track that hands over to the next one.
/// </summary>
/// <remarks>
/// <para>
/// A fade is scheduled rather than run in the caller: a game asks for one and forgets it, and the frame system that
/// owns the clock of the display advances what is scheduled. That is what lets a fade of the score outlast the frame
/// it was asked for without a game holding a timer of its own.
/// </para>
/// <para>
/// A second fade of the same channel or of the same playback replaces the first, because a game that interrupts itself
/// wants the second one: two fades of the same volume would fight over it rather than settle on a value.
/// </para>
/// <para>
/// Reaching a volume and ending a playback are two different things, so they have two names. <see cref="Fade(AudioChannel, float, float, TweenEasing)"/>
/// and <see cref="Fade(PlaybackHandle, float, float, TweenEasing)"/> only change the volume and leave the playback
/// running, which is what turning the score down and turning it back up asks for. <see cref="FadeAndStop"/> fades to
/// silence and stops the playback when the fade reaches the end, which is what a sound that has to go away rather than
/// stay silent asks for.
/// </para>
/// </remarks>
public interface IAudioFader
{
    /// <summary>Starts a fade of a channel from the volume it holds now to another one.</summary>
    /// <param name="channel">The channel to fade.</param>
    /// <param name="to">The volume to reach.</param>
    /// <param name="duration">How long the fade takes, in seconds. A duration of zero sets the volume at once.</param>
    /// <param name="easing">The shape of the change between the two volumes.</param>
    void Fade(AudioChannel channel, float to, float duration, TweenEasing easing = TweenEasing.Linear);

    /// <summary>Starts a fade of one playback from the volume it holds now to another one, leaving the playback running.</summary>
    /// <param name="playback">The handle of the playback.</param>
    /// <param name="to">The volume to reach.</param>
    /// <param name="duration">How long the fade takes, in seconds. A duration of zero sets the volume at once.</param>
    /// <param name="easing">The shape of the change between the two volumes.</param>
    /// <remarks>A handle the device does not hold fades nothing, which is what a game that kept one across the end of its sound does.</remarks>
    void Fade(PlaybackHandle playback, float to, float duration, TweenEasing easing = TweenEasing.Linear);

    /// <summary>Fades a playback to silence and stops it when the fade reaches the end.</summary>
    /// <param name="playback">The handle of the playback.</param>
    /// <param name="duration">How long the fade takes, in seconds. A duration of zero stops the playback at once.</param>
    /// <param name="easing">The shape of the change from the volume the playback holds now to silence.</param>
    /// <remarks>
    /// This is what a sound that has to go away rather than stay silent asks for: a click of an interface that is faded
    /// out rather than cut off, a track that hands over to the next one and stops when it is no longer heard. The
    /// volume of the playback reaches zero on the frame the fade ends, and the voice it was using is returned to the
    /// device on the same frame, so a game that plays a long list of sounds does not hold voices for the ones that are
    /// not heard any more.
    /// </remarks>
    void FadeAndStop(PlaybackHandle playback, float duration, TweenEasing easing = TweenEasing.Linear);

    /// <summary>Stops every fade that is scheduled for a channel, leaving its volume at whatever it reached.</summary>
    /// <param name="channel">The channel whose fades stop.</param>
    /// <returns>The number of fades that were stopped.</returns>
    int Cancel(AudioChannel channel);

    /// <summary>Stops every fade that is scheduled for a playback, leaving its volume at whatever it reached and leaving the playback running.</summary>
    /// <param name="playback">The handle of the playback whose fades stop.</param>
    /// <returns>The number of fades that were stopped.</returns>
    int Cancel(PlaybackHandle playback);
}