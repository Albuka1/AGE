using Age.Core;

namespace Age.UI;

/// <summary>
/// Marks a UI hierarchy root and holds the scaler of the interface of a world.
/// </summary>
/// <remarks>
/// A hierarchy is not here yet, so a canvas holds the resolution that its interface was authored against rather than the
/// elements below it: the layout measures every anchored element of the world against the canvas that <see cref="IsRoot"/>
/// marks, and against the window itself, at a scale of one, when a world has no canvas at all.
/// </remarks>
/// <example>
/// <code>
/// Entity canvas = world.CreateEntity();
/// world.Set(canvas, new CanvasComponent
/// {
///     IsRoot = true,
///     DesignSize = new Vector2(1280f, 720f),
///     ScaleMode = CanvasScaleMode.ScaleWithScreenSize,
///     Match = 0.5f,
/// });
/// </code>
/// </example>
[Component("Canvas")]
public struct CanvasComponent : IComponent
{
    /// <summary>Gets or sets a value indicating whether this canvas is the root of its hierarchy.</summary>
    public bool IsRoot;

    /// <summary>Gets or sets the resolution that the interface is authored against, in design units.</summary>
    /// <remarks>An element that is anchored to the whole canvas fills the window when this has the same shape as the window, whatever the size of either of them is.</remarks>
    public Vector2 DesignSize;

    /// <summary>Gets or sets how the canvas follows the size of the window.</summary>
    public CanvasScaleMode ScaleMode;

    /// <summary>Gets or sets which axis the scale of the canvas follows, from zero for the width to one for the height.</summary>
    /// <remarks>
    /// A canvas that matches the width fits the whole design across the screen and lets the other axis fall where it may; one
    /// that matches the height does the opposite; a value between the two blends the ratios, so half way between them is a scale
    /// that is as much over one axis as it is under the other. It is read only by
    /// <see cref="CanvasScaleMode.ScaleWithScreenSize"/>.
    /// </remarks>
    public float Match;

    /// <summary>Gets or sets the scale that the layout resolved, which is how many pixels of the screen a design unit is.</summary>
    public float Scale;

    /// <summary>Gets or sets the size of the canvas in design units, which the layout resolved.</summary>
    /// <remarks>It is the size of the window divided by <see cref="Scale"/>, so it is the design area that the window shows: a canvas of 1280 by 720 scaled to a window of 1920 by 1080 resolves to 1280 by 720 again.</remarks>
    public Vector2 Resolution;

    /// <summary>Resolves the scale of the canvas and the size of it in design units from the size of the window.</summary>
    /// <param name="screenSize">The size of the window, in pixels.</param>
    /// <remarks>
    /// A canvas that a game does not scale resolves to a scale of one and to the size of the window, which is what makes the
    /// resolved fields of a canvas without a scaler the same as the screen. A size of zero or less is treated as one pixel, so a
    /// window that is not on screen yet lays nothing out rather than dividing by nothing.
    /// </remarks>
    public void Resolve(Vector2 screenSize)
    {
        float width = MathF.Max(screenSize.X, 1f);
        float height = MathF.Max(screenSize.Y, 1f);

        if (ScaleMode != CanvasScaleMode.ScaleWithScreenSize || !(DesignSize.X > 0f) || !(DesignSize.Y > 0f))
        {
            Scale = 1f;
            Resolution = new Vector2(width, height);
            return;
        }

        float match = Math.Clamp(Match, 0f, 1f);
        float byWidth = width / DesignSize.X;
        float byHeight = height / DesignSize.Y;

        // The scale is the two ratios blended as a geometric mean, so the value half way between them is the one that leaves as
        // much of the design over one axis as it takes from the other: width^(1-match) * height^match.
        Scale = MathF.Pow(byWidth, 1f - match) * MathF.Pow(byHeight, match);
        Resolution = new Vector2(width / Scale, height / Scale);
    }
}

