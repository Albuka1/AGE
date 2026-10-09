namespace Age.Rendering;

/// <summary>
/// Where a line of text is placed against the width of the box that holds it.
/// </summary>
/// <remarks>
/// The alignment of a line that is wider than the box changes nothing, because such a line already fills it or overflows
/// it: how a line that does not fit is treated is <see cref="TextWrap"/> and <see cref="TextOverflow"/>.
/// </remarks>
public enum TextAlign
{
    /// <summary>The line starts at the left edge of the box, which is what a line of a world says.</summary>
    Start,

    /// <summary>The line is centered in the box, which is what a title or the text of a button says.</summary>
    Center,

    /// <summary>The line ends at the right edge of the box.</summary>
    End,
}
