using Age.Core;

namespace Age.UI;

/// <summary>
/// Positions and sizes a UI element in screen space, in pixels, with the origin at the top-left corner of the window.
/// </summary>
/// <remarks>
/// <para>
/// Screen space means the element is placed independently of the world camera and the y axis grows downwards. Point
/// tests such as the ones in <see cref="UIUpdateSystem"/> use the same coordinates, so a pointer position can be
/// compared with <see cref="Position"/> directly.
/// </para>
/// <para>
/// An element that leaves <see cref="Anchored"/> alone stands where <see cref="Position"/> and <see cref="Size"/> say, which is
/// a count of pixels and does not follow the resolution of the display. An element that sets it is placed by the anchors
/// instead: the layout resolves <see cref="Position"/> and <see cref="Size"/> from them, the canvas and the scale of it, so an
/// element that is anchored to the bottom-right corner of the canvas stays in that corner in a window of any size. Those two
/// fields are what the pass of the UI and the pointer tests read either way, so what a game reads after a frame is the
/// rectangle the element ended up with.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // A panel that keeps its distance from the top-left corner of the canvas, whatever the resolution is.
/// world.Set(panel, new RectTransformComponent
/// {
///     Anchored = true,
///     AnchorMin = Vector2.Zero,
///     AnchorMax = Vector2.Zero,
///     Pivot = Vector2.Zero,
///     AnchoredPosition = new Vector2(24f, 24f),
///     SizeDelta = new Vector2(200f, 50f),
///     Visible = true,
/// });
///
/// // A shade that covers the whole canvas, with the anchors of the two opposite corners.
/// world.Set(shade, new RectTransformComponent
/// {
///     Anchored = true,
///     AnchorMax = new Vector2(1f, 1f),
///     Visible = true,
/// });
/// </code>
/// </example>
[Component("RectTransform")]
public struct RectTransformComponent : IComponent
{
    /// <summary>Gets or sets the position of the top-left corner of the element, in screen pixels.</summary>
    /// <remarks>An anchored element has what the layout resolved here, so a game reads it rather than authors it.</remarks>
    public Vector2 Position;

    /// <summary>Gets or sets the size.</summary>
    /// <remarks>An anchored element has what the layout resolved here, which is <see cref="SizeDelta"/> added to the room between the anchors.</remarks>
    public Vector2 Size;

    /// <summary>Gets or sets the draw order. Larger values draw on top.</summary>
    public int ZOrder;

    /// <summary>Gets or sets a value indicating whether the element is drawn.</summary>
    public bool Visible;

    /// <summary>Gets or sets a value indicating whether the anchors place and size the element rather than the literal position and size of it.</summary>
    public bool Anchored;

    /// <summary>Gets or sets the corner of the canvas that the element is placed against: zero is its left or top edge and one its right or bottom edge.</summary>
    public Vector2 AnchorMin;

    /// <summary>Gets or sets the corner of the canvas that the other corner of the element is placed against: zero is the left or top edge and one the right or bottom one.</summary>
    /// <remarks>
    /// This corner and <see cref="AnchorMin"/> are the two normalized corners of the canvas that the element is placed in, so the room it
    /// is laid out in is the rectangle between them rather than an offset one of them adds to the other. An element that leaves this
    /// equal to <see cref="AnchorMin"/> has a point anchor and keeps its size; an element that gives it another corner stretches with
    /// the canvas, which is what a background or a full-screen shade needs.
    /// </remarks>
    public Vector2 AnchorMax;

    /// <summary>Gets or sets the point of the element that sits on the anchors: zero is its top-left corner and one its bottom-right corner.</summary>
    public Vector2 Pivot;

    /// <summary>Gets or sets where the pivot stands relative to the anchors, in design units.</summary>
    /// <remarks>A positive value moves the element to the right and down, which is the direction the axes of the screen grow in.</remarks>
    public Vector2 AnchoredPosition;

    /// <summary>Gets or sets the size of the element in design units, which is added to the room between the anchors.</summary>
    /// <remarks>
    /// A point anchor makes this the size of the element. Two different anchors make it the amount by which the element is
    /// larger or smaller than the room between them, so zero stretches it exactly across them and a margin is a negative value.
    /// </remarks>
    public Vector2 SizeDelta;

    /// <summary>Resolves <see cref="Position"/> and <see cref="Size"/> from the anchors, the canvas and the scale of it.</summary>
    /// <param name="canvas">The rectangle of the canvas in design units: its origin is the top-left corner of the screen and its size is the resolution the interface was authored against.</param>
    /// <param name="scale">The scale of the canvas, which is how many pixels of the screen a design unit is.</param>
    /// <remarks>
    /// The anchors cut the canvas into the room the element is placed in, the pivot chooses the point of that room the element
    /// hangs from, and <see cref="AnchoredPosition"/> moves the element away from it. Everything a game authored is in design
    /// units and everything written here is in pixels of the screen, so the same values lay the same interface out on a display
    /// of any resolution.
    /// </remarks>
    public void Resolve(Rect canvas, float scale)
    {
        Vector2 room = (AnchorMax - AnchorMin) * canvas.Size;
        Vector2 reference = canvas.Position + (AnchorMin * canvas.Size) + (room * Pivot);

        Size = (room + SizeDelta) * scale;
        Position = (reference * scale) + (AnchoredPosition * scale) - (Pivot * Size);
    }
}

