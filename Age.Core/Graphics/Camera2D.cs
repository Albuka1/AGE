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
        EnsureZoom();

        return Matrix4x4.CreateTranslation(-Position.X, -Position.Y, 0f) * Matrix4x4.CreateScale(1f / Zoom, 1f / Zoom, 1f);
    }

    /// <summary>Returns the world position that a point of the screen shows.</summary>
    /// <param name="screen">A point of the screen, in pixels, with the origin at the top-left corner of the viewport.</param>
    /// <returns>The position in the world that this point of the screen shows.</returns>
    /// <exception cref="InvalidOperationException">Zoom is NaN, zero or negative.</exception>
    /// <remarks>
    /// <para>
    /// This is the inverse of <see cref="WorldToScreen"/> and it is what a click becomes before a game asks what stands where:
    /// the pointer of <c>IInputService</c> is a point of the screen, a cell of a map is a box of the world, and the two meet in
    /// this call. A camera that moved or zoomed between the frame the pointer was read in and this call answers for where it
    /// stands now, because nothing of the conversion is remembered.
    /// </para>
    /// <para>
    /// The conversion is the inverse of the matrix the renderer draws with — the view of this camera followed by the
    /// orthographic projection of the viewport — so what a game reads here is what a player sees, down to the rounding: a point
    /// that a pass drew at a world position comes back as that position, and a point of the screen that no draw covered comes
    /// back as a position of the world that is outside everything the camera looked at.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// if (input.IsMouseButtonPressed(MouseButton.Left))
    /// {
    ///     Vector2 world = camera.ScreenToWorld(input.MousePosition);
    ///     Entity? clicked = OccupantOf(world);
    /// }
    /// </code>
    /// </example>
    public readonly Vector2 ScreenToWorld(Vector2 screen)
    {
        EnsureZoom();

        // The projection maps the view of the camera onto the viewport, so the way back is the position of the point in the
        // viewport, scaled by the zoom, moved by where the camera stands. Nothing of it depends on the size of the viewport,
        // which is why a viewport size that a game never set still converts: see VisibleWorld.
        return Position + (screen * Zoom);
    }

    /// <summary>Returns the point of the screen that a world position is drawn at.</summary>
    /// <param name="world">A position in the world.</param>
    /// <returns>The point of the screen, in pixels, with the origin at the top-left corner of the viewport.</returns>
    /// <exception cref="InvalidOperationException">Zoom is NaN, zero or negative.</exception>
    /// <remarks>
    /// This is the inverse of <see cref="ScreenToWorld"/>, and it is what a game places an interface of the world with: a name
    /// over a unit, a health bar under it, an arrow at the edge of the screen that points at something off it. A point outside
    /// the viewport is answered with the point it would be drawn at rather than refused, so a game that clamps an element to the
    /// edge reads the position and clamps it itself.
    /// </remarks>
    /// <example>
    /// <code>
    /// Vector2 corner = camera.WorldToScreen(unit.Position);
    ///
    /// renderer.DrawText(name, corner - new Vector2(0f, 18f), Color.White);
    /// </code>
    /// </example>
    public readonly Vector2 WorldToScreen(Vector2 world)
    {
        EnsureZoom();

        // The view matrix scales the world by one over the zoom, and Vector2 has no division by a number: the same reciprocal
        // that GetViewMatrix hands to Matrix4x4.CreateScale is what this multiplies by, so the two cannot disagree.
        return (world - Position) * (1f / Zoom);
    }

    /// <summary>Refuses a zoom that a conversion cannot be computed with.</summary>
    /// <exception cref="InvalidOperationException">Zoom is NaN, zero or negative.</exception>
    private readonly void EnsureZoom()
    {
        if (float.IsNaN(Zoom) || Zoom <= 0f)
        {
            throw new InvalidOperationException("Camera2D.Zoom must be greater than zero.");
        }
    }

    /// <summary>Returns a camera that shows a design area of the world in a window of the given size.</summary>
    /// <param name="designSize">The area of the world that the game is authored against, in world units.</param>
    /// <param name="windowSize">The size of the window, in pixels.</param>
    /// <param name="fit">Which part of the design area stays in view when the window has another shape than the area does.</param>
    /// <returns>A camera that measures the window and carries the zoom that fits the area to it.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The design size or the window size is zero, negative or not a number.</exception>
    /// <remarks>
    /// A camera that leaves the zoom at one shows one unit of the world per pixel of the window, so a larger window shows more
    /// of the world and a display that scales draws it smaller than it looks. This is the other way round: the game is authored
    /// against an area of a fixed size and the whole of that area is what a player sees, whatever the resolution of the display
    /// and whatever the shape of the window are, so a game that lays its map out for a window of 1280 by 720 looks the same at
    /// 3840 by 2160 and at 1280 by 800. The zoom is the ratio of the design area to the window, because the world is scaled by
    /// one over it: what the returned camera covers is <see cref="VisibleWorld"/>, which is the area a game reads when it lays a
    /// map out or places an interface of the world. A camera that covers the window crops the area, and the crop is shared by
    /// both of the opposite sides of it, so what a game places at the middle of the area is at the middle of the window at any
    /// shape of it; a camera that contains the area shows the whole of it from the origin of the world, with the room that is
    /// left over below and to the right of the area.
    /// </remarks>
    /// <example>
    /// <code>
    /// Camera2D camera = Camera2D.Fit(new Vector2(1280f, 720f), renderer.ViewportSize);
    ///
    /// renderPipeline.Render(world, camera);
    /// </code>
    /// </example>
    public static Camera2D Fit(Vector2 designSize, Vector2 windowSize, CameraFit fit = CameraFit.Contain)
    {
        if (!(designSize.X > 0f) || !(designSize.Y > 0f))
        {
            throw new ArgumentOutOfRangeException(nameof(designSize), designSize, "A design area is wider and taller than nothing.");
        }

        if (!(windowSize.X > 0f) || !(windowSize.Y > 0f))
        {
            throw new ArgumentOutOfRangeException(nameof(windowSize), windowSize, "A window is wider and taller than nothing.");
        }

        float byWidth = designSize.X / windowSize.X;
        float byHeight = designSize.Y / windowSize.Y;
        bool covers = fit == CameraFit.Cover;
        float zoom = covers ? MathF.Min(byWidth, byHeight) : MathF.Max(byWidth, byHeight);

        return new Camera2D
        {
            // A camera that covers the window crops the design area, so the camera is moved by half of what the crop takes off
            // and what a game places at the middle of the area stays at the middle of the window. A camera that contains the
            // area stands at the origin, because the whole of the area is in view: the room that is left over is outside of it
            // and is not part of the world the game was authored against.
            Position = covers ? (designSize - (windowSize * zoom)) * 0.5f : Vector2.Zero,
            ViewportSize = windowSize,
            Zoom = zoom,
        };
    }
}
