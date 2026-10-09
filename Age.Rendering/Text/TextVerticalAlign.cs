namespace Age.Rendering;

/// <summary>
/// Where the block of lines of a text is placed against the height of the box that holds it.
/// </summary>
/// <remarks>
/// A box with no height is as tall as the text, so every one of these looks the same in it: the alignment matters for a
/// box that a game sized itself, such as the row of a list or a panel of a fixed height.
/// </remarks>
public enum TextVerticalAlign
{
    /// <summary>The first line starts at the top of the box, which is what a paragraph says.</summary>
    Top,

    /// <summary>The block of lines is centered in the box, which is what the text of a button says.</summary>
    Middle,

    /// <summary>The last line ends at the bottom of the box.</summary>
    Bottom,
}
