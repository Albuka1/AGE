namespace Age.Rendering;

/// <summary>
/// What happens to the lines of a text that do not fit the height of the box that holds it.
/// </summary>
/// <remarks>
/// A box with no height is as tall as the text, so nothing overflows it and every one of these looks the same. A line that
/// is dropped is dropped whole: a box that cuts a line in half down the middle needs a pass that clips, which is not what
/// the layout of a text does.
/// </remarks>
public enum TextOverflow
{
    /// <summary>Every line is drawn, even the ones that are outside the box, which is the default.</summary>
    Visible,

    /// <summary>The lines that do not fit are dropped, and the last one that fits ends with an ellipsis.</summary>
    Ellipsis,

    /// <summary>The lines that do not fit are dropped.</summary>
    Clip,
}
