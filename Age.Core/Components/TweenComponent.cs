using System.Text.Json.Serialization;

namespace Age.Core;

/// <summary>The shape of the value a tween reports between its start and its end.</summary>
public enum TweenEasing
{
    /// <summary>The value moves at the same speed from the start to the end.</summary>
    Linear,

    /// <summary>The value starts slowly and speeds up.</summary>
    EaseIn,

    /// <summary>The value starts quickly and slows down.</summary>
    EaseOut,

    /// <summary>The value starts slowly, speeds up, and slows down again.</summary>
    EaseInOut,
}

/// <summary>
/// Moves a value from one number to another over a duration, which a <see cref="TweenSystem"/> advances.
/// </summary>
/// <remarks>
/// A tween does not know what its value is for: it computes it and announces it through <see cref="TweenUpdatedEvent"/>,
/// and the game writes it where it belongs. That keeps the component free of a delegate, which a scene could not save
/// and which a build without reflection could not describe. <see cref="Value"/> is also readable at any time, so a
/// game that only needs the value where it already is does not have to subscribe at all.
/// </remarks>
/// <example>
/// <code>
/// world.Set(sprite, TweenComponent.Between(0f, MathF.Tau, 3f, looping: true));
/// </code>
/// </example>
[Component("Tween")]
public struct TweenComponent : IComponent
{
    /// <summary>Gets or sets the value the tween starts at.</summary>
    public float From { get; set; }

    /// <summary>Gets or sets the value the tween ends at.</summary>
    public float To { get; set; }

    /// <summary>Gets or sets how long one run takes, in seconds.</summary>
    public float Duration { get; set; }

    /// <summary>Gets or sets the time that has passed of the current run, in seconds. The system writes it.</summary>
    public float Elapsed { get; set; }

    /// <summary>Gets or sets a value indicating whether the tween starts over at its end instead of stopping there.</summary>
    public bool Looping { get; set; }

    /// <summary>Gets or sets the shape of the value between the two ends.</summary>
    public TweenEasing Easing { get; set; }

    /// <summary>Gets or sets a value indicating whether the tween stands still and keeps its value.</summary>
    public bool Paused { get; set; }

    /// <summary>Gets the fraction of the run that has passed, between zero and one.</summary>
    [JsonIgnore]
    public readonly float Progress => Duration > 0f ? Math.Clamp(Elapsed / Duration, 0f, 1f) : 1f;

    /// <summary>Gets the value of the tween at the point it has reached, with <see cref="Easing"/> applied.</summary>
    [JsonIgnore]
    public readonly float Value => From + ((To - From) * Ease(Progress));

    /// <summary>Gets a value indicating whether a tween that does not loop has reached its end.</summary>
    [JsonIgnore]
    public readonly bool Completed => !Looping && Progress >= 1f;

    /// <summary>Returns a tween that runs from one value to another over a duration.</summary>
    /// <param name="from">The value to start at.</param>
    /// <param name="to">The value to end at.</param>
    /// <param name="duration">The length of one run, in seconds.</param>
    /// <param name="looping">A value indicating whether the tween starts over at its end.</param>
    /// <param name="easing">The shape of the value between the two ends.</param>
    /// <returns>The tween, ready to be attached to an entity.</returns>
    public static TweenComponent Between(float from, float to, float duration, bool looping = false, TweenEasing easing = TweenEasing.Linear) =>
        new() { From = from, To = to, Duration = duration, Looping = looping, Easing = easing };

    private readonly float Ease(float progress) => Easing switch
    {
        TweenEasing.EaseIn => progress * progress,
        TweenEasing.EaseOut => 1f - ((1f - progress) * (1f - progress)),
        TweenEasing.EaseInOut => progress < 0.5f ? 2f * progress * progress : 1f - (2f * (1f - progress) * (1f - progress)),
        _ => progress,
    };
}
