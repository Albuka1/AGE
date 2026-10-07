using Age.Core;

namespace Age.Rendering;

/// <summary>
/// Builds the four corners of a sprite quad, rotated around its centre.
/// </summary>
/// <remarks>
/// The corners come out in the order that the triangle strip of the renderer expects: top-left, top-right, bottom-left
/// and bottom-right before the rotation is applied, which keeps the mapping of the texture corners onto the vertices the
/// same whatever the angle.
/// </remarks>
public static class SpriteQuad
{
    /// <summary>Fills four corners with the quad of a sprite.</summary>
    /// <param name="position">The top-left corner of the unrotated quad, in world coordinates.</param>
    /// <param name="size">The size of the quad, in world coordinates.</param>
    /// <param name="rotation">The rotation around the centre of the quad, in radians.</param>
    /// <param name="corners">The span that receives the corners. At least four elements are written.</param>
    /// <remarks>A rotation of zero writes the corners of the rectangle itself, without going through the trigonometry.</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="corners"/> holds fewer than four elements.</exception>
    public static void Corners(Vector2 position, Vector2 size, float rotation, Span<Vector2> corners)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(corners.Length, 4);

        float halfWidth = size.X * 0.5f;
        float halfHeight = size.Y * 0.5f;

        if (rotation == 0f)
        {
            corners[0] = position;
            corners[1] = new Vector2(position.X + size.X, position.Y);
            corners[2] = new Vector2(position.X, position.Y + size.Y);
            corners[3] = new Vector2(position.X + size.X, position.Y + size.Y);
            return;
        }

        var centre = new Vector2(position.X + halfWidth, position.Y + halfHeight);
        float cosine = MathF.Cos(rotation);
        float sine = MathF.Sin(rotation);

        corners[0] = Rotate(-halfWidth, -halfHeight);
        corners[1] = Rotate(halfWidth, -halfHeight);
        corners[2] = Rotate(-halfWidth, halfHeight);
        corners[3] = Rotate(halfWidth, halfHeight);

        Vector2 Rotate(float offsetX, float offsetY) =>
            new(centre.X + (offsetX * cosine) - (offsetY * sine), centre.Y + (offsetX * sine) + (offsetY * cosine));
    }

    /// <summary>Returns the axis-aligned box that covers the quad, whatever its angle.</summary>
    /// <param name="position">The top-left corner of the unrotated quad, in world coordinates.</param>
    /// <param name="size">The size of the quad, in world coordinates.</param>
    /// <param name="rotation">The rotation around the centre of the quad, in radians.</param>
    /// <returns>A box that covers the quad, which a render system culls with.</returns>
    public static Aabb Bounds(Vector2 position, Vector2 size, float rotation)
    {
        if (rotation == 0f)
        {
            return Aabb.FromRect(new Rect(position, size));
        }

        Span<Vector2> corners = stackalloc Vector2[4];
        Corners(position, size, rotation, corners);

        float left = corners[0].X;
        float top = corners[0].Y;
        float right = corners[0].X;
        float bottom = corners[0].Y;

        for (int index = 1; index < corners.Length; index++)
        {
            left = MathF.Min(left, corners[index].X);
            top = MathF.Min(top, corners[index].Y);
            right = MathF.Max(right, corners[index].X);
            bottom = MathF.Max(bottom, corners[index].Y);
        }

        return Aabb.FromRect(new Rect(new Vector2(left, top), new Vector2(right - left, bottom - top)));
    }
}
