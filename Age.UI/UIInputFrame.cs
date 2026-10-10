namespace Age.UI;

/// <summary>
/// What one control of the interface did with the click of a frame, which is what the controls of the frame after it read.
/// </summary>
/// <remarks>
/// <para>
/// A press is one event of the frame, and more than one control may be under the pointer: an open drop-down list hangs over the
/// elements below it, and a text field may stand under a row of that list. The control that takes a click is the one that stands
/// over the others, so what one system consumed has to be known by the next: without this, the same press would choose a row of a
/// list and take the focus of a field that the list covered.
/// </para>
/// <para>
/// The state belongs to the container rather than to a world, because the systems that read it are singletons of the container and
/// the answer is about a frame. It is reset at the start of every frame by the system that reads the pointer first, so a decision of
/// one frame is never read as the decision of the next.
/// </para>
/// </remarks>
public sealed class UIInputFrame
{
    /// <summary>Gets a value indicating whether a control took the click of this frame, which is what keeps it from reaching the rest of the interface.</summary>
    public bool Consumed { get; private set; }

    /// <summary>Reports that a control took the click of this frame.</summary>
    public void Consume() => Consumed = true;

    /// <summary>Begins a frame, which is what forgets what the frame before it took.</summary>
    public void Begin() => Consumed = false;
}
