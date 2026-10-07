namespace Age.Core;

/// <summary>
/// A two-dimensional camera. The view matrix translates the world by -Position and then scales it by 1/Zoom.
/// </summary>
public struct Camera2D
{
    /// <summary>Gets or sets the world position that maps to the viewport origin.</summary>
    public Vector2 Position { get; set; }

    /// <summary>Gets or sets the zoom factor. Must be greater than zero.</summary>
    public float Zoom { get; set; }

    /// <summary>Gets or sets the size of the frame that the camera looks at, in pixels.</summary>
    /// <remarks>
    /// The projection of the renderer is built from the viewport of the window, so this is not the size of the picture: it
    /// is the size that <see cref="VisibleWorld"/> and the culling of the render systems use, and a game keeps it in step
    /// with the window it draws into. A camera that leaves it at zero covers no known rectangle and culls nothing.
    /// </remarks>
    public Vector2 ViewportSize { get; set; }

    /// <summary>Gets the rectangle of the world that the camera looks at.</summary>
    /// <remarks>
    /// The rectangle starts at <see cref="Position"/> and covers <see cref="ViewportSize"/> scaled by <see cref="Zoom"/>,
    /// because the view matrix scales the world down by one over the zoom: a camera with a larger zoom covers more of the
    /// world and shows it smaller. A camera without a viewport size reports an empty rectangle, which the render systems
    /// treat as "cull nothing".
    /// </remarks>
    public readonly Rect VisibleWorld => new(Position, new Vector2(ViewportSize.X * Zoom, ViewportSize.Y * Zoom));

    /// <summary>Returns the view matrix of the camera. The projection is not part of it; the renderer composes the view with its own orthographic projection.</summary>
    /// <returns>The view matrix: translate(-Position) followed by scale(1/Zoom).</returns>
    /// <exception cref="InvalidOperationException">Zoom is NaN, zero or negative.</exception>
    /// <remarks>
    /// Vectors are transformed as rows, so the product applies the translation first, which maps <see cref="Position"/>
    /// to the origin of view space at any zoom. The projection of the renderer then places that origin at the top-left
    /// corner of the viewport. With a zero position and a unit zoom the result is the identity matrix.
    /// </remarks>
    public readonly Matrix4x4 GetViewMatrix()
    {
        if (float.IsNaN(Zoom) || Zoom <= 0f)
        {
            throw new InvalidOperationException("Camera2D.Zoom must be greater than zero.");
        }

        return Matrix4x4.CreateTranslation(-Position.X, -Position.Y, 0f) * Matrix4x4.CreateScale(1f / Zoom, 1f / Zoom, 1f);
    }
}
