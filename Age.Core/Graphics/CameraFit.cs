namespace Age.Core;

/// <summary>
/// Says which part of a design area a camera keeps in view when the window has another shape than the area does.
/// </summary>
/// <remarks>
/// The two of them differ only in what is left over when the shapes of the design area and of the window are not the same: one
/// of the two shows a strip of the world that the other crops, and which of those a game wants is what it says here.
/// </remarks>
public enum CameraFit
{
    /// <summary>The whole design area is in view, and the axis that has room left over shows more of the world.</summary>
    Contain,

    /// <summary>The design area fills the window, and the axis that has no room is cropped on both of its ends.</summary>
    Cover,
}
