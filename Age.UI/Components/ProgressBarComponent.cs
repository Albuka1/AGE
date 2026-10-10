using Age.Core;

namespace Age.UI;

/// <summary>
/// Makes a UI element a progress bar: a number drawn as a filled part of a track, which reports how far a thing has come without
/// letting anyone change it.
/// </summary>
/// <remarks>
/// <para>
/// It is a slider without a handle and without a drag: the pass that draws the interface fills <see cref="Normalized"/> of the
/// rectangle, and the pointer is not read. A loading bar, a health bar and a cooldown are all this.
/// </para>
/// <para>
/// The number is clamped to the range whenever it is written, exactly as a slider, and a range of no length answers zero.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// world.Set(bar, new RectTransformComponent { Anchored = true, SizeDelta = new Vector2(240f, 12f), Visible = true });
/// world.Set(bar, new ProgressBarComponent { Minimum = 0f, Maximum = 1f, Value = progress });
/// </code>
/// </example>
[Component("ProgressBar")]
public struct ProgressBarComponent : IComponent
{
    private float _value;

    /// <summary>Gets or sets the number the filled part reaches, which is the low end of the range.</summary>
    public float Minimum;

    /// <summary>Gets or sets the number a full bar reaches, which is the high end of the range.</summary>
    public float Maximum;

    /// <summary>Gets or sets the number the bar reports, which is kept inside the range.</summary>
    public float Value
    {
        readonly get => _value;
        set => _value = Math.Clamp(value, MathF.Min(Minimum, Maximum), MathF.Max(Minimum, Maximum));
    }

    /// <summary>Gets or sets where the value stands in the range, from zero at <see cref="Minimum"/> to one at <see cref="Maximum"/>.</summary>
    /// <remarks>A range of no length, which two equal ends make, answers zero rather than dividing by nothing.</remarks>
    public float Normalized
    {
        readonly get
        {
            float span = Maximum - Minimum;
            return MathF.Abs(span) < float.Epsilon ? 0f : Math.Clamp((_value - Minimum) / span, 0f, 1f);
        }

        set => Value = Minimum + (Math.Clamp(value, 0f, 1f) * (Maximum - Minimum));
    }
}
