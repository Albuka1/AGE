namespace Age.UI;

/// <summary>
/// Says how a canvas follows the size of the window.
/// </summary>
/// <remarks>
/// A game that authors its interface against a resolution of its own picks <see cref="ScaleWithScreenSize"/>, so the same
/// layout fits a window of any size; a game that thinks in pixels of the screen picks <see cref="ConstantPixelSize"/> and gets
/// the window it was written for, with whatever the display does on top of it.
/// </remarks>
public enum CanvasScaleMode
{
    /// <summary>A unit of the interface is a pixel of the window, whatever the resolution of the display is.</summary>
    ConstantPixelSize,

    /// <summary>The interface is authored against <see cref="CanvasComponent.DesignSize"/> and the whole of it is scaled to the window.</summary>
    ScaleWithScreenSize,
}
