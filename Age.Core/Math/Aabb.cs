namespace Age.Core;

/// <summary>
/// Represents an axis-aligned bounding box.
/// </summary>
public readonly struct Aabb
{
    private Aabb(float left, float top, float right, float bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    /// <summary>Gets the x coordinate of the left edge.</summary>
    public float Left { get; }

    /// <summary>Gets the y coordinate of the top edge.</summary>
    public float Top { get; }

    /// <summary>Gets the x coordinate of the right edge.</summary>
    public float Right { get; }

    /// <summary>Gets the y coordinate of the bottom edge.</summary>
    public float Bottom { get; }

    /// <summary>Gets the width of the box, in units.</summary>
    public float Width => Right - Left;

    /// <summary>Gets the height of the box, in units.</summary>
    public float Height => Bottom - Top;

    /// <summary>Creates a bounding box that exactly covers the rectangle.</summary>
    public static Aabb FromRect(Rect rect) => new(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);

    /// <summary>Determines whether this box strictly overlaps another box. Touching edges are not a collision.</summary>
    public bool Intersects(Aabb other) =>
        Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;

    /// <summary>
    /// Returns the shortest translation that moves this box out of another one, or zero when the two do not overlap.
    /// </summary>
    /// <param name="other">The box this one is moved out of.</param>
    /// <returns>
    /// The translation to add to this box so that it no longer overlaps <paramref name="other"/>, along the axis that is
    /// cheaper to separate them by. A box that does not overlap answers zero.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The shortest of the four ways out is the one a person reads as the direction the thing was pushed from, which is
    /// what a resolution of two boxes is: a body that lands on the top of another is moved up rather than to the side,
    /// because the distance to the top is smaller than the distance to the left or the right.
    /// </para>
    /// <para>
    /// Two boxes that share an edge do not overlap and answer zero, which is what keeps a resolution that runs after a
    /// previous one from moving a body that is already out of another.
    /// </para>
    /// </remarks>
    public Vector2 Penetration(Aabb other)
    {
        if (!Intersects(other))
        {
            return Vector2.Zero;
        }

        float left = other.Left - Right;
        float right = other.Right - Left;
        float top = other.Top - Bottom;
        float bottom = other.Bottom - Top;

        // The two horizontal ways are the ones that move this box off the other side of it: a positive x moves the box to
        // the right and a negative one to the left, so the axis and the direction of the shortest way are the two answers
        // this method gives.
        float horizontal = MathF.Abs(left) < MathF.Abs(right) ? left : right;
        float vertical = MathF.Abs(top) < MathF.Abs(bottom) ? top : bottom;

        return MathF.Abs(horizontal) < MathF.Abs(vertical)
            ? new Vector2(horizontal, 0f)
            : new Vector2(0f, vertical);
    }

    /// <summary>Returns the box translated by a vector.</summary>
    /// <param name="offset">The translation to add to every edge.</param>
    /// <returns>The box in its new place, with the same size.</returns>
    public Aabb MovedBy(Vector2 offset) =>
        new(Left + offset.X, Top + offset.Y, Right + offset.X, Bottom + offset.Y);
}
