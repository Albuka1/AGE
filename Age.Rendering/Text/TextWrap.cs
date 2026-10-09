namespace Age.Rendering;

/// <summary>
/// How a line of text that is wider than the box is broken into lines that fit it.
/// </summary>
/// <remarks>
/// A box with no width has no edge to break at, so a text that is laid out into one keeps the lines it was written with,
/// which is what a line of a world does. A word that is longer than the box cannot be broken at a space, so it is only
/// broken by <see cref="Anywhere"/>; otherwise it overflows the box.
/// </remarks>
public enum TextWrap
{
    /// <summary>A line is broken at a space, and a word that does not fit is left to overflow.</summary>
    Word,

    /// <summary>A line is broken at a space, and a word that does not fit is broken wherever the box ends.</summary>
    Anywhere,

    /// <summary>A line is broken only where the text says so.</summary>
    None,
}
