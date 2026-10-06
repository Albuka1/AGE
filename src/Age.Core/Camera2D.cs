namespace Age.Core;

/// <summary>
/// A two-dimensional camera. The view matrix is scale(1/Zoom) multiplied by translate(-Position).
/// </summary>
public struct Camera2D
{
    /// <summary>Gets or sets the world position that maps to the viewport origin.</summary>
    public Vector2 Position { get; set; }

    /// <summary>Gets or sets the zoom factor. Must be greater than zero.</summary>
    public float Zoom { get; set; }

    /// <summary>Gets or sets the viewport size, in pixels.</summary>
    public Vector2 ViewportSize { get; set; }

    /// <summary>
    /// Returns the view matrix: scale(1/Zoom) multiplied by translate(-Position). With a zero position and unit zoom the result is the identity matrix.
    /// </summary>
    public readonly Matrix4x4 GetViewMatrix()
    {
        if (Zoom <= 0f)
        {
            throw new InvalidOperationException("Camera2D.Zoom must be greater than zero.");
        }

        return Matrix4x4.CreateScale(1f / Zoom, 1f / Zoom, 1f) * Matrix4x4.CreateTranslation(-Position.X, -Position.Y, 0f);
    }
}
