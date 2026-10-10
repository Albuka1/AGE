using Age.Core;

namespace Age.UI;

/// <summary>
/// Makes a UI element a slider: a number the player drags along a track, which a settings screen and a bar are both made of.
/// </summary>
/// <remarks>
/// <para>
/// The component holds the number and the range it lies in; the pass that draws the interface reads <see cref="Value"/> and
/// <see cref="Normalized"/> to place the handle. A press inside the rectangle of the element begins a drag, a held pointer moves the
/// number, and letting go ends the drag, so a slipped pointer is followed rather than abandoning the value.
/// </para>
/// <para>
/// The number is clamped to the range whenever it is written, so a game reads a value inside it rather than testing the bounds itself.
/// A range whose ends are equal is one number rather than a division by zero: <see cref="Normalized"/> answers zero.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// world.Set(volume, new RectTransformComponent { Anchored = true, SizeDelta = new Vector2(200f, 20f), Visible = true });
/// world.Set(volume, new SliderComponent { Minimum = 0f, Maximum = 1f, Value = 0.5f, Interactable = true });
/// </code>
/// </example>
[Component("Slider")]
public struct SliderComponent : IComponent
{
    private float _value;

    /// <summary>Gets or sets the lowest number the slider holds.</summary>
    public float Minimum;

    /// <summary>Gets or sets the highest number the slider holds.</summary>
    public float Maximum;

    /// <summary>Gets or sets the number the slider holds.</summary>
    /// <remarks>
    /// What is stored is the number as it was written and what is read is that number kept inside the range, so the authored value
    /// survives however the fields were assigned: a document that writes <c>Value</c> before <c>Minimum</c> and <c>Maximum</c> reads the
    /// value it wrote once the range is there, rather than the range it was clamped against when the range was still zero.
    /// </remarks>
    public float Value
    {
        readonly get => Math.Clamp(_value, MathF.Min(Minimum, Maximum), MathF.Max(Minimum, Maximum));
        set => _value = value;
    }

    /// <summary>Gets or sets a value indicating whether the slider reacts to input.</summary>
    public bool Interactable;

    /// <summary>Gets or sets a value indicating whether the pointer is currently dragging the handle.</summary>
    public bool IsDragging;

    /// <summary>Gets or sets a value indicating whether the pointer is currently over the slider.</summary>
    public bool IsHovered;

    /// <summary>Gets or sets where the value stands in the range, from zero at <see cref="Minimum"/> to one at <see cref="Maximum"/>.</summary>
    /// <remarks>A range of no length, which two equal ends make, answers zero rather than dividing by nothing.</remarks>
    public float Normalized
    {
        readonly get
        {
            float span = Maximum - Minimum;
            return MathF.Abs(span) < float.Epsilon ? 0f : Math.Clamp((Value - Minimum) / span, 0f, 1f);
        }

        set => Value = Minimum + (Math.Clamp(value, 0f, 1f) * (Maximum - Minimum));
    }
}

/// <summary>
/// Raised while a slider is dragged, which is what a game reacts to rather than reading the value on every frame.
/// </summary>
/// <param name="Entity">The element that carries the slider.</param>
/// <param name="Value">The number the slider holds after the move.</param>
public readonly record struct SliderChangedEvent(Entity Entity, float Value);
